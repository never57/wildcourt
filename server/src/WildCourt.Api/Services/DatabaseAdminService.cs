using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;
using WildCourt.Api.Models;
using WildCourt.Api.Utils;

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
    private readonly bool _readOnly;
    private readonly bool _blockDrop;
    private readonly int _maxAffectedRows;
    private readonly HashSet<string> _sensitiveColumns;

    public DatabaseAdminService(IConfiguration config, ILogger<DatabaseAdminService> logger)
    {
        _connectionString = config["AdminDatabase:ConnectionString"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _connectionString = config.GetConnectionString("MySql")
                ?? "Server=localhost;Port=3306;Database=wildcourt;User=root;Password=;CharSet=utf8mb4;";
        }
        _logger = logger;
        _readOnly = config.GetValue("AdminDatabase:ReadOnly", false);
        _blockDrop = config.GetValue("AdminDatabase:BlockDrop", true);
        _maxAffectedRows = config.GetValue("AdminDatabase:MaxAffectedRows", 1000);
        var sensitive = config.GetSection("AdminDatabase:SensitiveColumns").Get<string[]>();
        _sensitiveColumns = new HashSet<string>(
            (sensitive ?? new[] { "password", "password_hash", "password_hash_hex", "password_hash_sha256", "token", "access_token" })
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0));
    }

    /// <summary>只读模式下拒绝一切写操作。</summary>
    private void EnsureWritable()
    {
        if (_readOnly)
        {
            throw new BusinessException(ApiCodes.Forbidden, "数据库已开启只读模式，写操作被禁用");
        }
    }

    /// <summary>连接串默认数据库名。</summary>
    private string GetDefaultDatabase()
    {
        try
        {
            return new MySqlConnectionStringBuilder(_connectionString).Database ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
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
            var guard = SqlGuard.Inspect(statement);

            // 1) 只读模式：仅放行查询类语句
            if (_readOnly && !guard.IsQuery)
            {
                throw new BusinessException(ApiCodes.Forbidden, "数据库已开启只读模式，只允许 SELECT 查询");
            }

            // 2) 危险语句拦截（DROP / TRUNCATE）
            if (_blockDrop && guard.IsDrop)
            {
                throw new BusinessException(ApiCodes.BadRequest, $"已拦截危险语句（{guard.Head}）：操作不可恢复，禁止执行");
            }

            // 3) 强制 WHERE：UPDATE / DELETE 必须带条件
            if (guard.IsUpdateOrDelete && !guard.HasWhere)
            {
                throw new BusinessException(ApiCodes.BadRequest, $"已拦截无 WHERE 条件的 {guard.Head}：拒绝执行（防误伤全表）");
            }

            // 4) 影响行数预估：UPDATE / DELETE 预计影响行数超上限则拒绝
            if (guard.IsUpdateOrDelete && guard.HasWhere)
            {
                var countSql = SqlGuard.BuildCountSql(statement);
                if (countSql is not null)
                {
                    try
                    {
                        var estimated = Convert.ToInt64(await ExecuteScalarAsync(conn, countSql));
                        if (estimated > _maxAffectedRows)
                        {
                            throw new BusinessException(ApiCodes.BadRequest,
                                $"已拦截 {guard.Head}：预计影响 {estimated} 行，超过上限 {_maxAffectedRows} 行");
                        }
                    }
                    catch (BusinessException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // 预估查询失败不阻断执行，仅记录（可能语句过于复杂）
                        _logger.LogWarning("影响行数预估失败，跳过行数检查：{Message}", ex.Message);
                    }
                }
            }

            // 5) 执行
            await using var cmd = new MySqlCommand(statement, conn);
            cmd.CommandTimeout = 120;
            var n = await cmd.ExecuteNonQueryAsync();
            affected += n;
            _logger.LogInformation("管理员执行 SQL：{Statement}（影响 {Rows} 行）", statement, n);
            if (guard.IsUpdateOrDelete && n > _maxAffectedRows)
            {
                _logger.LogWarning("管理员执行 {Head} 实际影响 {Rows} 行，超出上限 {Max}，请确认是否预期", guard.Head, n, _maxAffectedRows);
            }
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

    public async Task<TableDataResultDto> GetTableDataAsync(
        string database, string table, int page, int pageSize,
        List<RowFilterDto>? filters = null, string? orderBy = null, bool orderDesc = false)
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

        var (whereClause, whereParameters) = BuildWhereClause(filters);
        var orderClause = BuildOrderClause(orderBy, orderDesc);

        var offset = (page - 1) * pageSize;
        var selectSql = $"SELECT * FROM {fullTable}{whereClause} ORDER BY {orderClause} LIMIT @offset, @limit";

        var rows = new List<Dictionary<string, object?>>();
        long total = 0;

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();

        await using (var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM {fullTable}{whereClause}", conn))
        {
            foreach (var p in whereParameters)
            {
                countCmd.Parameters.Add(p);
            }
            total = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
        }

        await using var selectCmd = new MySqlCommand(selectSql, conn);
        foreach (var p in whereParameters)
        {
            selectCmd.Parameters.Add(p);
        }
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

        MaskSensitive(rows);

        return new TableDataResultDto
        {
            Columns = columns,
            Rows = rows,
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <summary>获取表结构：列 + 索引（含主键）。</summary>
    public async Task<TableSchemaResultDto> GetTableSchemaAsync(string database, string table)
    {
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new BusinessException(ApiCodes.BadRequest, "数据库名不能为空");
        }
        if (string.IsNullOrWhiteSpace(table))
        {
            throw new BusinessException(ApiCodes.BadRequest, "表名不能为空");
        }

        var columns = await GetColumnsAsync(database, table);
        var indexes = new List<IndexInfoDto>();

        const string indexSql = """
            SELECT s.INDEX_NAME, s.NON_UNIQUE,
                   GROUP_CONCAT(s.COLUMN_NAME ORDER BY s.SEQ_IN_INDEX)
            FROM information_schema.STATISTICS s
            WHERE s.TABLE_SCHEMA = @db AND s.TABLE_NAME = @table
            GROUP BY s.INDEX_NAME, s.NON_UNIQUE
            ORDER BY s.INDEX_NAME
            """;
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(indexSql, conn);
        cmd.Parameters.AddWithValue("@db", database);
        cmd.Parameters.AddWithValue("@table", table);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            indexes.Add(new IndexInfoDto
            {
                Name = name,
                Unique = reader.GetInt64(1) == 0,
                IsPrimary = name.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase),
                Columns = reader.GetString(2),
            });
        }

        return new TableSchemaResultDto
        {
            Database = database,
            Table = table,
            Columns = columns,
            Indexes = indexes,
        };
    }

    /// <summary>只读查询：仅放行单条 SELECT 语句，返回结果集。</summary>
    public async Task<QueryResultDto> QueryAsync(string sql)
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
            throw new BusinessException(ApiCodes.BadRequest, "SQL 内容为空");
        }
        if (statements.Count > 1)
        {
            throw new BusinessException(ApiCodes.BadRequest, "db_query 只允许单条 SELECT 语句");
        }

        var guard = SqlGuard.Inspect(statements[0]);
        if (!guard.IsQuery)
        {
            throw new BusinessException(ApiCodes.BadRequest, "db_query 只允许 SELECT 查询，其他操作请使用 execute-sql");
        }

        var result = new QueryResultDto { IsQuery = true };
        var maxRows = Math.Max(_maxAffectedRows, 100);
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(statements[0], conn);
        cmd.CommandTimeout = 120;
        await using var reader = await cmd.ExecuteReaderAsync();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            result.Columns.Add(reader.GetName(i));
        }
        while (await reader.ReadAsync())
        {
            if (result.Rows.Count >= maxRows)
            {
                break;
            }
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            }
            result.Rows.Add(row);
        }
        MaskSensitive(result.Rows);
        result.Total = result.Rows.Count;
        result.Message = $"返回 {result.Rows.Count} 行（上限 {maxRows} 行）";
        return result;
    }

    /// <summary>导出表数据：csv 或 json 格式。</summary>
    public async Task<(string FileName, string Content, string ContentType)> ExportAsync(string database, string table, string format)
    {
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new BusinessException(ApiCodes.BadRequest, "数据库名不能为空");
        }
        if (string.IsNullOrWhiteSpace(table))
        {
            throw new BusinessException(ApiCodes.BadRequest, "表名不能为空");
        }
        if (!format.Equals("csv", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ApiCodes.BadRequest, "导出格式仅支持 csv 或 json");
        }

        var columns = await GetColumnsAsync(database, table);
        var fullTable = $"{QuoteIdentifier(database)}.{QuoteIdentifier(table)}";
        var columnNames = columns.Select(c => c.Name).ToList();

        var rows = new List<Dictionary<string, object?>>();
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand($"SELECT * FROM {fullTable} LIMIT {_maxAffectedRows}", conn);
        cmd.CommandTimeout = 120;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }
        MaskSensitive(rows);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
            return ($"{table}.json", json, "application/json; charset=utf-8");
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columnNames.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", columnNames.Select(n => EscapeCsv(row.TryGetValue(n, out var v) ? v?.ToString() : null))));
        }
        return ($"{table}.csv", sb.ToString(), "text/csv; charset=utf-8");
    }

    public async Task<int> UpdateRowAsync(RowChangeRequest request)
    {
        EnsureWritable();
        ValidateChangeRequest(request);
        var pkColumn = await ResolvePrimaryKeyAsync(request);
        var fullTable = BuildFullTable(request.Database, request.Table);

        var sets = new List<string>();
        var parameters = new List<MySqlParameter>();
        var index = 0;
        foreach (var kv in request.Values)
        {
            sets.Add($"{QuoteIdentifier(kv.Key)} = @v{index}");
            parameters.Add(new MySqlParameter($"@v{index}", NormalizeValue(kv.Value) ?? DBNull.Value));
            index++;
        }

        parameters.Add(new MySqlParameter("@pk", NormalizeValue(request.PrimaryKeyValue) ?? DBNull.Value));
        var sql = $"UPDATE {fullTable} SET {string.Join(", ", sets)} WHERE {QuoteIdentifier(pkColumn)} = @pk";
        return await ExecuteNonQueryAsync(sql, parameters.ToArray());
    }

    public async Task<int> InsertRowAsync(RowChangeRequest request)
    {
        EnsureWritable();
        ValidateChangeRequest(request);
        var fullTable = BuildFullTable(request.Database, request.Table);

        var columns = new List<string>();
        var placeholders = new List<string>();
        var parameters = new List<MySqlParameter>();
        var index = 0;
        foreach (var kv in request.Values)
        {
            columns.Add(QuoteIdentifier(kv.Key));
            placeholders.Add($"@v{index}");
            parameters.Add(new MySqlParameter($"@v{index}", NormalizeValue(kv.Value) ?? DBNull.Value));
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
        EnsureWritable();
        ValidateChangeRequest(request);
        var pkColumn = await ResolvePrimaryKeyAsync(request);
        var fullTable = BuildFullTable(request.Database, request.Table);
        var sql = $"DELETE FROM {fullTable} WHERE {QuoteIdentifier(pkColumn)} = @pk";
        return await ExecuteNonQueryAsync(sql, new MySqlParameter("@pk", NormalizeValue(request.PrimaryKeyValue) ?? DBNull.Value));
    }

    // ==================== 私有辅助 ====================

    /// <summary>定位主键列：请求指定优先，否则从表结构自动识别。</summary>
    private async Task<string> ResolvePrimaryKeyAsync(RowChangeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PrimaryKey))
        {
            return request.PrimaryKey.Trim();
        }

        var database = request.Database ?? GetDefaultDatabase();
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new BusinessException(ApiCodes.BadRequest, "无法确定数据库，请显式指定 database 或主键列名");
        }
        var columns = await GetColumnsAsync(database, request.Table);
        var pk = columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name;
        if (pk is null)
        {
            throw new BusinessException(ApiCodes.BadRequest, $"表 {request.Table} 没有主键，请显式提供主键列名");
        }
        _logger.LogInformation("自动识别表 {Table} 主键为 {Pk}", request.Table, pk);
        return pk;
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

    private static async Task<object?> ExecuteScalarAsync(MySqlConnection conn, string sql)
    {
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.CommandTimeout = 120;
        return await cmd.ExecuteScalarAsync();
    }

    /// <summary>构造参数化 WHERE 子句。列名反引号包裹，运算符白名单校验，值全部参数化。</summary>
    private static (string Clause, List<MySqlParameter> Parameters) BuildWhereClause(List<RowFilterDto>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return (string.Empty, new List<MySqlParameter>());
        }

        var ops = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "=", "!=", "<>", ">", ">=", "<", "<=", "like", "in" };
        var parts = new List<string>();
        var parameters = new List<MySqlParameter>();
        var index = 0;

        foreach (var f in filters)
        {
            if (string.IsNullOrWhiteSpace(f.Column))
            {
                continue;
            }
            var op = f.Op.Trim();
            if (!ops.Contains(op))
            {
                throw new BusinessException(ApiCodes.BadRequest, $"不支持的过滤运算符：{f.Op}（可用：= != > >= < <= like in）");
            }
            var col = QuoteIdentifier(f.Column.Trim());

            if (op.Equals("in", StringComparison.OrdinalIgnoreCase))
            {
                var items = NormalizeInValues(f.Value);
                if (items is null || items.Count == 0)
                {
                    throw new BusinessException(ApiCodes.BadRequest, "in 运算符需要非空数组值");
                }
                var placeholders = new List<string>();
                foreach (var item in items)
                {
                    var p = $"@f{index++}";
                    placeholders.Add(p);
                    parameters.Add(new MySqlParameter(p, item ?? DBNull.Value));
                }
                parts.Add($"{col} IN ({string.Join(", ", placeholders)})");
                continue;
            }

            var param = $"@f{index++}";
            parts.Add($"{col} {op} {param}");
            parameters.Add(new MySqlParameter(param, NormalizeValue(f.Value) ?? DBNull.Value));
        }

        if (parts.Count == 0)
        {
            return (string.Empty, new List<MySqlParameter>());
        }
        return (" WHERE " + string.Join(" AND ", parts), parameters);
    }

    private static List<object?>? NormalizeInValues(object? value)
    {
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            return je.EnumerateArray().Select(v => NormalizeValue(v)).ToList();
        }
        if (value is IEnumerable<object?> list)
        {
            return list.ToList();
        }
        return null;
    }

    private static string BuildOrderClause(string? orderBy, bool orderDesc)
    {
        var direction = orderDesc ? "DESC" : "ASC";
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            // 未指定排序列时，MySQL 数字字面量按结果集第 1 列排序；
            // 不可用反引号包裹（`1` 会被当作列名导致 Unknown column）。
            return $"1 {direction}";
        }
        var column = orderBy.Trim();
        if (!Regex.IsMatch(column, @"^[A-Za-z0-9_]+$"))
        {
            throw new BusinessException(ApiCodes.BadRequest, "orderBy 只支持单列名（字母/数字/下划线）");
        }
        return $"{QuoteIdentifier(column)} {direction}";
    }

    /// <summary>对敏感列（密码哈希等）脱敏，避免回显给调用方。</summary>
    private void MaskSensitive(List<Dictionary<string, object?>> rows)
    {
        if (_sensitiveColumns.Count == 0 || rows.Count == 0)
        {
            return;
        }
        foreach (var row in rows)
        {
            foreach (var key in row.Keys.ToList())
            {
                if (_sensitiveColumns.Contains(key.ToLowerInvariant()) && row[key] != null)
                {
                    row[key] = "***";
                }
            }
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    private static void ValidateChangeRequest(RowChangeRequest request)
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
        if (!string.IsNullOrWhiteSpace(request.PrimaryKey))
        {
            if (request.PrimaryKey.Length > 64)
            {
                throw new BusinessException(ApiCodes.BadRequest, "主键列名不合法");
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

    /// <summary>
    /// 将 JSON 反序列化得到的 JsonElement 规范化为 MySqlConnector 可识别的 CLR 类型。
    /// Dictionary&lt;string, object?&gt; 反序列化时数字字段默认为 JsonElement，
    /// 直接绑定 MySqlParameter 会因无法推断 DbType 抛异常（表现为"服务器内部错误"）。
    /// </summary>
    private static object? NormalizeValue(object? value)
    {
        if (value is not JsonElement je)
        {
            return value;
        }
        switch (je.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.True:
            case JsonValueKind.False:
                return je.GetBoolean();
            case JsonValueKind.String:
                return je.GetString();
            case JsonValueKind.Number:
                // 整数优先 long，避免小数列被误转为整型丢失精度；
                // 超出 long 或带小数时依次尝试 decimal/double。
                if (je.TryGetInt64(out var longValue))
                {
                    return longValue;
                }
                if (je.TryGetDecimal(out var decimalValue))
                {
                    return decimalValue;
                }
                return je.GetDouble();
            default:
                // 数组/对象等复合值（如 JSON 列）按原始文本绑定
                return je.GetRawText();
        }
    }
}
