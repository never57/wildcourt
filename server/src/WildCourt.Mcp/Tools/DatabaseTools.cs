using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace WildCourt.Mcp.Tools;

/// <summary>数据库工具：库/表浏览、表数据分页、执行 SQL、行级增删改。高危接口仅供受信管理员。</summary>
[McpServerToolType]
public class DatabaseTools
{
    private static AdminApiClient _api = null!;

    public static Task InitializeAsync(AdminApiClient api)
    {
        _api = api;
        return Task.CompletedTask;
    }

    private static string Json(object value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>列出可操作的业务数据库（排除系统库）。</summary>
    [McpServerTool(Name = "db_list_databases"), Description("列出管理后台可操作的业务数据库（已排除系统库）。")]
    public async Task<string> ListDatabases()
    {
        var raw = await _api.GetAsync("database/databases");
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>列出指定数据库下的数据表。</summary>
    [McpServerTool(Name = "db_list_tables"), Description("列出指定数据库下的数据表（含表注释与行数）。")]
    public async Task<string> ListTables(
        [Description("数据库名")] string database)
    {
        var raw = await _api.GetAsync("database/tables", new Dictionary<string, string?> { ["database"] = database });
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>分页读取表数据（含列结构），默认第 1 页每页 50 行。支持过滤与排序。</summary>
    [McpServerTool(Name = "db_table_data"), Description("分页读取表数据，返回列结构 + 当前页数据 + 总行数。可选 filter 条件过滤、orderBy 单列排序。page 从 1 开始，pageSize 建议 ≤200。")]
    public async Task<string> TableData(
        [Description("数据库名")] string database,
        [Description("表名")] string table,
        [Description("页码，从 1 开始，默认 1")] int? page = null,
        [Description("每页行数，默认 50")] int? pageSize = null,
        [Description("过滤条件 JSON 数组，如 [{\"column\":\"user_id\",\"op\":\"=\",\"value\":1}]；op 支持 = != > >= < <= like in（in 时 value 为数组）")] string? filter = null,
        [Description("排序列名（单列），默认按主键/首列")] string? orderBy = null,
        [Description("是否倒序，默认 false")] bool? orderDesc = null)
    {
        var query = new Dictionary<string, string?>
        {
            ["database"] = database,
            ["table"] = table,
            ["page"] = (page ?? 1).ToString(),
            ["pageSize"] = (pageSize ?? 50).ToString(),
            ["orderDesc"] = (orderDesc ?? false) ? "true" : "false",
        };
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query["filter"] = filter;
        }
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            query["orderBy"] = orderBy;
        }
        var raw = await _api.GetAsync("database/table-data", query);
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>获取表结构：字段定义 + 索引（含主键）。</summary>
    [McpServerTool(Name = "db_table_schema"), Description("获取表结构：字段定义（类型/可空/主键/默认值/注释）+ 索引列表。写操作前建议先查结构确认列名。")]
    public async Task<string> TableSchema(
        [Description("数据库名")] string database,
        [Description("表名")] string table)
    {
        var raw = await _api.GetAsync("database/schema", new Dictionary<string, string?>
        {
            ["database"] = database,
            ["table"] = table,
        });
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>只读查询：仅允许单条 SELECT 语句。</summary>
    [McpServerTool(Name = "db_query"), Description("只读查询：仅允许单条 SELECT 语句，返回结果集。适合多表 JOIN、聚合统计等场景。")]
    public async Task<string> Query(
        [Description("只读 SELECT 语句，单条，如 SELECT COUNT(*) FROM users")] string sql)
    {
        var raw = await _api.PostAsync("database/query", new { Sql = sql });
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>导出表数据为 CSV 或 JSON 文本（敏感列自动脱敏）。</summary>
    [McpServerTool(Name = "db_export"), Description("导出表数据为 CSV 或 JSON 文本，敏感列自动脱敏。适合备份核对。")]
    public async Task<string> Export(
        [Description("数据库名")] string database,
        [Description("表名")] string table,
        [Description("导出格式：csv 或 json，默认 csv")] string? format = null)
    {
        var raw = await _api.GetRawAsync("database/export", new Dictionary<string, string?>
        {
            ["database"] = database,
            ["table"] = table,
            ["format"] = format ?? "csv",
        });
        return raw;
    }

    /// <summary>执行 SQL 语句（建表/改表/初始化/查询）。高危操作，请确认 SQL 内容正确。</summary>
    [McpServerTool(Name = "db_execute_sql"), Description("执行 SQL 语句（建表/改表/初始化数据/查询均可）。高危接口：执行前请向用户明确展示 SQL 内容并取得确认，仅执行 DDL/DML，避免 DROP 类不可逆操作。")]
    public async Task<string> ExecuteSql(
        [Description("要执行的 SQL 语句，可包含多条，以分号分隔")] string sql)
    {
        var raw = await _api.PostAsync("database/execute-sql", new { Sql = sql });
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>新增一行数据到指定表。</summary>
    [McpServerTool(Name = "db_insert_row"), Description("新增一行数据到指定表。Values 为 JSON 对象：列名->值；值为 null 表示 SQL NULL。")]
    public async Task<string> InsertRow(
        [Description("数据库名，为空用连接串默认库")] string? database,
        [Description("表名")] string table,
        [Description("JSON 对象：列名->值，如 {\"name\":\"张三\",\"gold\":100}")] string values)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(values, JsonOpts.Default)
            ?? throw new McpApiException("values 不是合法的 JSON 对象");
        var raw = await _api.PostAsync("database/row", new { Database = database, Table = table, Values = dict });
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>按主键更新一行数据。</summary>
    [McpServerTool(Name = "db_update_row"), Description("按主键更新一行数据。PrimaryKey 为主键列名（可省略，服务端自动识别表主键），PrimaryKeyValue 为主键值。Values 为 JSON 对象：列名->值。")]
    public async Task<string> UpdateRow(
        [Description("数据库名，为空用连接串默认库")] string? database,
        [Description("表名")] string table,
        [Description("JSON 对象：列名->值，如 {\"gold\":100}")] string values,
        [Description("主键列名，可省略（服务端自动识别）")] string? primaryKey = null,
        [Description("主键值")] string primaryKeyValue = "")
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(values, JsonOpts.Default)
            ?? throw new McpApiException("values 不是合法的 JSON 对象");
        var raw = await _api.PutAsync("database/row", new
        {
            Database = database,
            Table = table,
            Values = dict,
            PrimaryKey = primaryKey,
            PrimaryKeyValue = primaryKeyValue,
        });
        return Json(JsonDocument.Parse(raw).RootElement);
    }

    /// <summary>按主键删除一行数据（不可恢复，请谨慎）。</summary>
    [McpServerTool(Name = "db_delete_row"), Description("按主键删除一行数据。不可恢复：删除前请先读取该行确认无误，并向用户确认。PrimaryKey 可省略，服务端自动识别表主键。")]
    public async Task<string> DeleteRow(
        [Description("数据库名，为空用连接串默认库")] string? database,
        [Description("表名")] string table,
        [Description("主键列名，可省略（服务端自动识别）")] string? primaryKey = null,
        [Description("主键值")] string primaryKeyValue = "")
    {
        var raw = await _api.DeleteAsync("database/row", null, new
        {
            Database = database,
            Table = table,
            PrimaryKey = primaryKey,
            PrimaryKeyValue = primaryKeyValue,
        });
        return Json(JsonDocument.Parse(raw).RootElement);
    }
}
