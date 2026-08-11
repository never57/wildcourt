using System.Text;
using System.Text.RegularExpressions;
using WildCourt.Api.Models;

namespace WildCourt.Api.Utils;

/// <summary>
/// MySQL 连接管理 / 事务辅助（与 EF Core 并存）（文档 02 §2.7）。
/// </summary>
public class MySqlUtil
{
    // TODO: 连接管理 / 事务辅助（骨架阶段留空）。
}

/// <summary>
/// 简易 SQL 语句拆分器：识别单双引号、反引号、行注释/块注释，按分号拆分。
/// 支持大多数建表/改表 DDL；不支持 DELIMITER 自定义分隔符（存储过程体请用单条语句提交）。
/// </summary>
public static class StatementSplitter
{
    public static List<string> Split(string sql)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBacktick = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n')
                {
                    inLineComment = false;
                    sb.Append(c);
                }
                continue;
            }
            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }
                continue;
            }
            if (inSingleQuote)
            {
                sb.Append(c);
                if (c == '\\' && next != '\0')
                {
                    sb.Append(next);
                    i++;
                }
                else if (c == '\'')
                {
                    inSingleQuote = false;
                }
                continue;
            }
            if (inDoubleQuote)
            {
                sb.Append(c);
                if (c == '\\' && next != '\0')
                {
                    sb.Append(next);
                    i++;
                }
                else if (c == '"')
                {
                    inDoubleQuote = false;
                }
                continue;
            }
            if (inBacktick)
            {
                sb.Append(c);
                if (c == '`')
                {
                    inBacktick = false;
                }
                continue;
            }

            switch (c)
            {
                case '-' when next == '-':
                    inLineComment = true;
                    i++;
                    break;
                case '/' when next == '*':
                    inBlockComment = true;
                    i++;
                    break;
                case '\'':
                    inSingleQuote = true;
                    sb.Append(c);
                    break;
                case '"':
                    inDoubleQuote = true;
                    sb.Append(c);
                    break;
                case '`':
                    inBacktick = true;
                    sb.Append(c);
                    break;
                case ';':
                    result.Add(sb.ToString());
                    sb.Clear();
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}

/// <summary>
/// 静态 SQL 守卫：识别语句类型、危险语句（DROP/TRUNCATE）、是否带 WHERE，
/// 并为 UPDATE/DELETE 构建影响行数预估的 COUNT 查询。
/// 仅做语法层防护，目的：避免误操作不可逆地破坏数据。
/// </summary>
public static class SqlGuard
{
    public sealed record Inspection(string Head, bool IsQuery, bool IsDrop, bool IsUpdateOrDelete, bool HasWhere);

    private static readonly string[] QueryHeads = { "SELECT", "WITH", "SHOW", "DESC", "DESCRIBE", "EXPLAIN" };
    private static readonly string[] DropHeads = { "DROP", "TRUNCATE" };

    public static Inspection Inspect(string statement)
    {
        var head = GetHeadKeyword(statement);
        var isQuery = QueryHeads.Contains(head);
        var isDrop = DropHeads.Contains(head);
        var isUpdateOrDelete = head == "UPDATE" || head == "DELETE";
        var hasWhere = ContainsKeyword(statement, "WHERE");
        return new Inspection(head, isQuery, isDrop, isUpdateOrDelete, hasWhere);
    }

    /// <summary>
    /// 为单表 UPDATE/DELETE 构建行数预估查询；无法可靠解析时返回 null（调用方跳过检查）。
    /// </summary>
    public static string? BuildCountSql(string statement)
    {
        var s = statement.Trim().TrimEnd(';').Trim();
        var upper = s.ToUpperInvariant();

        string table;
        string wherePart;
        if (upper.StartsWith("DELETE FROM", StringComparison.OrdinalIgnoreCase))
        {
            var rest = s.Substring("DELETE FROM".Length).Trim();
            var whereIdx = FindKeywordIndex(rest, "WHERE");
            if (whereIdx < 0)
            {
                return null;
            }
            table = rest[..whereIdx].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            wherePart = rest[(whereIdx + "WHERE".Length)..].Trim();
        }
        else if (upper.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            var rest = s.Substring("UPDATE".Length).Trim();
            var setIdx = FindKeywordIndex(rest, "SET");
            var whereIdx = FindKeywordIndex(rest, "WHERE");
            if (setIdx < 0 || whereIdx < 0 || whereIdx < setIdx)
            {
                return null;
            }
            table = rest[..setIdx].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            wherePart = rest[(whereIdx + "WHERE".Length)..].Trim();
        }
        else
        {
            return null;
        }

        if (table.Length == 0 || wherePart.Length == 0)
        {
            return null;
        }
        if (!Regex.IsMatch(table, @"^[A-Za-z0-9_.`]+$"))
        {
            return null;
        }
        return $"SELECT COUNT(*) FROM {table} WHERE {wherePart}";
    }

    private static string GetHeadKeyword(string statement)
    {
        // 跳过开头注释（--、#、/* */）与空白
        var s = statement.TrimStart();
        while (s.StartsWith("--", StringComparison.Ordinal) || s.StartsWith('#'))
        {
            var nl = s.IndexOf('\n');
            if (nl < 0)
            {
                return string.Empty;
            }
            s = s[(nl + 1)..].TrimStart();
        }
        if (s.StartsWith("/*", StringComparison.Ordinal))
        {
            var end = s.IndexOf("*/", 2, StringComparison.Ordinal);
            if (end < 0)
            {
                return string.Empty;
            }
            s = s[(end + 2)..].TrimStart();
        }
        var firstWord = new string(s.TakeWhile(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        return firstWord.ToUpperInvariant();
    }

    private static bool ContainsKeyword(string sql, string keyword)
    {
        return FindKeywordIndex(sql, keyword) >= 0;
    }

    private static int FindKeywordIndex(string sql, string keyword)
    {
        var upper = sql.ToUpperInvariant();
        var idx = upper.IndexOf(keyword, StringComparison.Ordinal);
        while (idx >= 0)
        {
            var beforeOk = idx == 0 || !IsWordChar(upper[idx - 1]);
            var afterIdx = idx + keyword.Length;
            var afterOk = afterIdx >= upper.Length || !IsWordChar(upper[afterIdx]);
            if (beforeOk && afterOk)
            {
                return idx;
            }
            idx = upper.IndexOf(keyword, idx + 1, StringComparison.Ordinal);
        }
        return -1;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
