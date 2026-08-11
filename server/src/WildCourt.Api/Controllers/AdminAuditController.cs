using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 操作审计日志：分页查询 /api/v1/admin 写操作留痕。
/// 写入由 AdminAuditMiddleware 自动完成，本控制器只读。
/// </summary>
public class AdminAuditController : AdminControllerBase
{
    private readonly AdminAuditService _service;

    public AdminAuditController(AdminAuditService service)
    {
        _service = service;
    }

    /// <summary>分页查询审计日志。</summary>
    [HttpGet("audit-logs")]
    public async Task<ApiResponse<PagedResult<AdminOperationLogDto>>> Query([FromQuery] AuditLogQueryRequest query)
    {
        return ApiResponse<PagedResult<AdminOperationLogDto>>.Ok(await _service.QueryAsync(query));
    }

    /// <summary>所有出现过的操作名（筛选下拉用）。</summary>
    [HttpGet("audit-actions")]
    public async Task<ApiResponse<List<string>>> Actions()
    {
        return ApiResponse<List<string>>.Ok(await _service.GetActionsAsync());
    }
}
