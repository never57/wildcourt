using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 数据库管理：执行运营上传的 SQL（建表/改表/初始化数据）。
/// 高危接口：仅限受信管理员，执行前务必在后台二次确认。
/// </summary>
public class AdminDatabaseController : AdminControllerBase
{
    private readonly DatabaseAdminService _database;

    public AdminDatabaseController(DatabaseAdminService database)
    {
        _database = database;
    }

    /// <summary>执行 JSON 提交的 SQL（sql 或 sqlFile 字段二选一）。</summary>
    [HttpPost("database/execute-sql")]
    public async Task<ApiResponse<ExecuteSqlResultDto>> ExecuteSql([FromBody] ExecuteSqlRequest request)
    {
        var sql = request.SqlFile ?? request.Sql;
        return ApiResponse<ExecuteSqlResultDto>.Ok(await _database.ExecuteAsync(sql ?? string.Empty));
    }

    /// <summary>上传 .sql 文件并执行（multipart/form-data，字段名 file）。</summary>
    [HttpPost("database/upload-sql")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ApiResponse<ExecuteSqlResultDto>> UploadSql(IFormFile file)
    {
        if (file.Length == 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "上传文件为空");
        }
        using var reader = new StreamReader(file.OpenReadStream());
        var sql = await reader.ReadToEndAsync();
        return ApiResponse<ExecuteSqlResultDto>.Ok(await _database.ExecuteAsync(sql));
    }

    // ==================== 可视化浏览与编辑 ====================

    /// <summary>列出可操作的业务数据库（排除系统库）。</summary>
    [HttpGet("database/databases")]
    public async Task<ApiResponse<List<DatabaseInfoDto>>> ListDatabases()
    {
        return ApiResponse<List<DatabaseInfoDto>>.Ok(await _database.ListDatabasesAsync());
    }

    /// <summary>列出指定数据库下的表。</summary>
    [HttpGet("database/tables")]
    public async Task<ApiResponse<List<TableInfoDto>>> ListTables([FromQuery] string database)
    {
        return ApiResponse<List<TableInfoDto>>.Ok(await _database.ListTablesAsync(database));
    }

    /// <summary>分页读取表数据（含列结构）。</summary>
    [HttpGet("database/table-data")]
    public async Task<ApiResponse<TableDataResultDto>> GetTableData(
        [FromQuery] string database,
        [FromQuery] string table,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return ApiResponse<TableDataResultDto>.Ok(await _database.GetTableDataAsync(database, table, page, pageSize));
    }

    /// <summary>更新一行（按主键定位）。</summary>
    [HttpPut("database/row")]
    public async Task<ApiResponse<int>> UpdateRow([FromBody] RowChangeRequest request)
    {
        return ApiResponse<int>.Ok(await _database.UpdateRowAsync(request));
    }

    /// <summary>新增一行。</summary>
    [HttpPost("database/row")]
    public async Task<ApiResponse<int>> InsertRow([FromBody] RowChangeRequest request)
    {
        return ApiResponse<int>.Ok(await _database.InsertRowAsync(request));
    }

    /// <summary>删除一行（按主键定位）。</summary>
    [HttpDelete("database/row")]
    public async Task<ApiResponse<int>> DeleteRow([FromBody] RowChangeRequest request)
    {
        return ApiResponse<int>.Ok(await _database.DeleteRowAsync(request));
    }
}
