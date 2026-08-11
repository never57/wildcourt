using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>运营后台仪表盘：玩家规模 + 注册趋势。</summary>
public class AdminDashboardController : AdminControllerBase
{
    private readonly AdminService _adminService;

    public AdminDashboardController(AdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>统计摘要（玩家总数/今日新增/在线/活跃/货币总量/近 7 日注册趋势）。</summary>
    [HttpGet("dashboard/summary")]
    public async Task<ApiResponse<DashboardSummaryResponse>> Summary()
    {
        RequirePermission(AdminPermissions.DashboardView);
        return ApiResponse<DashboardSummaryResponse>.Ok(await _adminService.GetDashboardSummaryAsync());
    }
}
