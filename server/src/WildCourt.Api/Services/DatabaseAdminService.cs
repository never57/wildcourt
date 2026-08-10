using System.Diagnostics;
using System.Text;
using MySqlConnector;
using WildCourt.Api.Models;

namespace WildCourt.Api.Services;

/// <summary>
/// 数据库管理：执行运营上传的 DDL/DML（建表、改表等）。
/// 连接串优先 AdminDatabase:ConnectionString，缺省回退 ConnectionStrings:MySql。
/// 注意：该接口拥有完整库级权限，仅限内网/受信管理员调用（AdminAuthMiddleware 保护）。
/// </summary>
public class DatabaseAdminService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseAdminService> _logger;

    public DatabaseAdminService(IConfiguration config, ILogger<DatabaseAdminService> logger)
    {
        _connectionString = config["AdminDatabase:ConnectionString"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _connectionString = config.GetConnectionString("MySql")
                ?? "Server=localhost;Port=3306;Database=wildcourt;User=root;Password=;CharSet=utf8mb4;";
        }
        _logger = logger;
    }

    public async Task<ExecuteSqlResultDto> ExecuteAsync(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new BusinessException(ApiCodes.BadRequest, "SQL 内容为空");
        }

        var statements = StatementSplitter.Split(sql)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("--"))
            .ToList();
        if (statements.Count == 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "未识别到可执行的 SQL 语句");
        }

        long affected = 0;
        var sw = Stopwatch.StartNew();
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        foreach (var statement in statements)
        {
            await using var cmd = new MySqlCommand(statement, conn);
            cmd.CommandTimeout = 120;
            affected += await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("管理员执行 SQL：{Statement}", statement);
        }
        sw.Stop();

        return new ExecuteSqlResultDto
        {
            StatementCount = statements.Count,
            AffectedRows = affected,
            DurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1),
            Message = $"成功执行 {statements.Count} 条语句，影响 {affected} 行，耗时 {sw.Elapsed.TotalSeconds:F1}s",
        };
    }

    // ==================== 可视化浏览与编辑 ====================

    public async Task<List<DatabaseInfoDto>> ListDatabasesAsync()
    {
        const string sql = """
            SELECT SCHEMA_NAME FROM information_schema.SCHEMATA
            WHERE SCHEMA_NAME NOT IN ('information_schema','performance_schema','mysql','sys')
            ORDER BY SCHEMA_NAME
            """;
        var result = new List<DatabaseInfoDto>();
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new DatabaseInfoDto { Name = reader.GetString(0) });
        }
        return result;
    }

    public async Task<List<TableInfoDto>> ListTablesAsync(string database)
    {
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new BusinessException(ApiCodes.BadRequest, "数据库名不能为空");
        }

        const string sql = """
            SELECT t.TABLE_NAME, COALESCE(t.TABLE_COMMENT, ''), COALESCE(t.TABLE_ROWS, 0)
            FROM information_schema.TABLES t
            WHERE t.TABLE_SCHEMA = @db AND t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_NAME
            """;
        var result = new List<TableInfoDto>();
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@db", database);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new TableInfoDto
            {
                Name = reader.GetString(0),
                Comment = reader.GetString(1),
                Rows = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
            });
        }
        return result;
    }

    public async Task<TableDataResultDto> GetTableDataAsync(string database, string table, int page, int pageSize)
    {
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new BusinessException(ApiCodes.BadRequest, "数据库名不能为空");
        }
        if (string.IsNullOrWhiteSpace(table))
        {
            throw new BusinessException(ApiCodes.BadRequest, "表名不能为空");
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var columns = await GetColumnsAsync(database, table);
        var fullTable = $"{QuoteIdentifier(database)}.{QuoteIdentifier(table)}";
        var pkColumn = columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name;

        var offset = (page - 1) * pageSize;
        var selectSql = pkColumn is null
            ? $"SELECT * FROM {fullTable} LIMIT @offset, @limit"
            : $"SELECT * FROM {fullTable} ORDER BY {QuoteIdentifier(pkColumn)} LIMIT @offset, @limit";

        var rows = new List<Dictionary<string, object?>>();
        long total = 0;

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();

        await using (var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM {fullTable}", conn))
        {
            total = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
        }

        await using var selectCmd = new MySqlCommand(selectSql, conn);
        selectCmd.Parameters.AddWithValue("@offset", offset);
        selectCmd.Parameters.AddWithValue("@limit", pageSize);
        await using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return new TableDataResultDto
        {
            Columns = columns,
            Rows = rows,
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<int> UpdateRowAsync(RowChangeRequest request)
    {
        ValidateChangeRequest(request, requirePrimaryKey: true);
        var fullTable = BuildFullTable(request.Database, request.Table);

        var sets = new List<string>();
        var parameters = new List<MySqlParameter>();
        var index = 0;
        foreach (var kv in request.Values)
        {
            sets.Add($"{QuoteIdentifier(kv.Key)} = @v{index}");
            parameters.Add(new MySqlParameter($"@v{index}", kv.Value ?? DBNull.Value));
            index++;
        }

        parameters.Add(new MySqlParameter("@pk", request.PrimaryKeyValue ?? DBNull.Value));
        var sql = $"UPDATE {fullTable} SET {string.Join(", ", sets)} WHERE {QuoteIdentifier(request.PrimaryKey!)} = @pk";
        return await ExecuteNonQueryAsync(sql, parameters.ToArray());
    }

    public async Task<int> InsertRowAsync(RowChangeRequest request)
    {
        ValidateChangeRequest(request, requirePrimaryKey: false);
        var fullTable = BuildFullTable(request.Database, request.Table);

        var columns = new List<string>();
        var placeholders = new List<string>();
        var parameters = new List<MySqlParameter>();
        var index = 0;
        foreach (var kv in request.Values)
        {
            columns.Add(QuoteIdentifier(kv.Key));
            placeholders.Add($"@v{index}");
            parameters.Add(new MySqlParameter($"@v{index}", kv.Value ?? DBNull.Value));
            index++;
        }
        if (columns.Count == 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "没有需要插入的字段");
        }

        var sql = $"INSERT INTO {fullTable} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", placeholders)})";
        return await ExecuteNonQueryAsync(sql, parameters.ToArray());
    }

    public async Task<int> DeleteRowAsync(RowChangeRequest request)
    {
        ValidateChangeRequest(request, requirePrimaryKey: true);
        var fullTable = BuildFullTable(request.Database, request.Table);
        var sql = $"DELETE FROM {fullTable} WHERE {QuoteIdentifier(request.PrimaryKey!)} = @pk";
        return await ExecuteNonQueryAsync(sql, new MySqlParameter("@pk", request.PrimaryKeyValue ?? DBNull.Value));
    }

    private async Task<List<ColumnInfoDto>> GetColumnsAsync(string database, string table)
    {
        const string sql = """
            SELECT c.COLUMN_NAME, c.COLUMN_TYPE, c.IS_NULLABLE, c.COLUMN_KEY,
                   c.COLUMN_DEFAULT, COALESCE(c.COLUMN_COMMENT, '')
            FROM information_schema.COLUMNS c
            WHERE c.TABLE_SCHEMA = @db AND c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION
            """;
        var result = new List<ColumnInfoDto>();
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@db", database);
        cmd.Parameters.AddWithValue("@table", table);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ColumnInfoDto
            {
                Name = reader.GetString(0),
                ColumnType = reader.GetString(1),
                Nullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase),
                IsPrimaryKey = string.Equals(reader.GetString(3), "PRI", StringComparison.OrdinalIgnoreCase),
                DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                Comment = reader.GetString(5),
            });
        }
        return result;
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, params MySqlParameter[] parameters)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.CommandTimeout = 120;
        foreach (var parameter in parameters)
        {
            cmd.Parameters.Add(parameter);
        }
        _logger.LogInformation("管理员执行 DML：{Sql}", sql);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static void ValidateChangeRequest(RowChangeRequest request, bool requirePrimaryKey)
    {
        if (string.IsNullOrWhiteSpace(request.Table) || request.Table.Length > 64)
        {
            throw new BusinessException(ApiCodes.BadRequest, "表名不合法");
        }
        if (request.Database is { Length: > 64 })
        {
            throw new BusinessException(ApiCodes.BadRequest, "数据库名不合法");
        }
        if (request.Values.Count > 200)
        {
            throw new BusinessException(ApiCodes.BadRequest, "一次最多更新 200 个字段");
        }
        if (requirePrimaryKey)
        {
            if (string.IsNullOrWhiteSpace(request.PrimaryKey) || request.PrimaryKey.Length > 64)
            {
                throw new BusinessException(ApiCodes.BadRequest, "缺少主键列名");
            }
            if (request.Values.ContainsKey(request.PrimaryKey))
            {
                throw new BusinessException(ApiCodes.BadRequest, "不允许修改主键字段");
            }
        }
    }

    private static string BuildFullTable(string? database, string table)
    {
        return string.IsNullOrWhiteSpace(database)
            ? QuoteIdentifier(table)
            : $"{QuoteIdentifier(database)}.{QuoteIdentifier(table)}";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "`" + identifier.Replace("`", "``") + "`";
    }
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
