using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>管理平台：举报审理（列表/详情/处理 + 封禁/解封玩家聊天）。</summary>
public class AdminReportController : AdminControllerBase
{
    private readonly ReportService _reportService;

    public AdminReportController(ReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>举报列表（可按状态筛选 + 分页）。</summary>
    [HttpGet("reports")]
    public async Task<ApiResponse<PagedResult<AdminReportListItemDto>>> List([FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        RequirePermission(AdminPermissions.ReportsView);
        var result = await _reportService.ListReportsAsync(status, Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
        return ApiResponse<PagedResult<AdminReportListItemDto>>.Ok(result);
    }

    /// <summary>举报详情（含两人全部聊天记录）。</summary>
    [HttpGet("reports/{id:long}")]
    public async Task<ApiResponse<AdminReportDetailDto>> Detail(long id)
    {
        RequirePermission(AdminPermissions.ReportsView);
        return ApiResponse<AdminReportDetailDto>.Ok(await _reportService.GetReportDetailAsync(id));
    }

    /// <summary>处理举报：标记已处理；chatBanDays &gt; 0 封禁被举报人聊天 N 天，=0 不封禁。</summary>
    [HttpPost("reports/{id:long}/resolve")]
    public async Task<ApiResponse<object>> Resolve(long id, [FromBody] ResolveReportRequest request)
    {
        RequirePermission(AdminPermissions.ReportsEdit);
        await _reportService.ResolveAsync(GetAdminId(), id, request.ChatBanDays, request.Note);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>封禁玩家聊天 N 天（days &lt;=0 解封）。</summary>
    [HttpPost("players/{userId:long}/chat-ban")]
    public async Task<ApiResponse<object>> ChatBan(long userId, [FromBody] SetChatBanRequest request)
    {
        RequirePermission(AdminPermissions.ReportsEdit);
        var changed = await _reportService.SetChatBanAsync(GetAdminId(), userId, request.Days, request.Reason);
        return ApiResponse<object>.Ok(new { Changed = changed });
    }

    /// <summary>解封玩家聊天。</summary>
    [HttpPost("players/{userId:long}/chat-unban")]
    public async Task<ApiResponse<object>> ChatUnban(long userId)
    {
        RequirePermission(AdminPermissions.ReportsEdit);
        var changed = await _reportService.SetChatBanAsync(GetAdminId(), userId, 0);
        return ApiResponse<object>.Ok(new { Changed = changed });
    }
}
