using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>玩家管理：列表/搜索/详情、货币等级修改、封禁/解封。</summary>
public class AdminPlayerController : AdminControllerBase
{
    private readonly AdminService _adminService;

    public AdminPlayerController(AdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>玩家分页搜索（keyword 匹配 账号/昵称/UserId）。</summary>
    [HttpGet("players")]
    public async Task<ApiResponse<PagedResult<PlayerAdminDto>>> List([FromQuery] AdminPlayerSearchRequest request)
    {
        RequirePermission(AdminPermissions.PlayersView);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        return ApiResponse<PagedResult<PlayerAdminDto>>.Ok(
            await _adminService.SearchPlayersAsync(request.Keyword, page, pageSize));
    }

    /// <summary>玩家详情（含宠物/背包/好友数）。</summary>
    [HttpGet("players/{userId}")]
    public async Task<ApiResponse<PlayerDetailDto>> Detail(long userId)
    {
        RequirePermission(AdminPermissions.PlayersView);
        return ApiResponse<PlayerDetailDto>.Ok(await _adminService.GetPlayerDetailAsync(userId));
    }

    /// <summary>修改玩家货币/等级/经验（覆盖语义）。</summary>
    [HttpPut("players/{userId}/currency")]
    public async Task<ApiResponse<object>> UpdateCurrency(long userId, [FromBody] UpdatePlayerCurrencyRequest request)
    {
        RequirePermission(AdminPermissions.PlayersEdit);
        await _adminService.UpdatePlayerCurrencyAsync(userId, request);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>修改玩家昵称/头像等基础资料。</summary>
    [HttpPut("players/{userId}/profile")]
    public async Task<ApiResponse<object>> UpdateProfile(long userId, [FromBody] UpdatePlayerProfileRequest request)
    {
        RequirePermission(AdminPermissions.PlayersEdit);
        await _adminService.UpdatePlayerProfileAsync(userId, request);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>封禁玩家（踢出全部会话）。</summary>
    [HttpPost("players/{userId}/ban")]
    public async Task<ApiResponse<object>> Ban(long userId)
    {
        RequirePermission(AdminPermissions.PlayersBan);
        var changed = await _adminService.BanAsync(userId);
        return ApiResponse<object>.Ok(new { Changed = changed });
    }

    /// <summary>解封玩家。</summary>
    [HttpPost("players/{userId}/unban")]
    public async Task<ApiResponse<object>> Unban(long userId)
    {
        RequirePermission(AdminPermissions.PlayersBan);
        var changed = await _adminService.UnbanAsync(userId);
        return ApiResponse<object>.Ok(new { Changed = changed });
    }

    /// <summary>批量封禁玩家。</summary>
    [HttpPost("players/batch-ban")]
    public async Task<ApiResponse<BatchOperationResult>> BatchBan([FromBody] BatchBanRequest request)
    {
        RequirePermission(AdminPermissions.PlayersBan);
        return ApiResponse<BatchOperationResult>.Ok(await _adminService.BanBatchAsync(request.UserIds));
    }

    /// <summary>批量解封玩家。</summary>
    [HttpPost("players/batch-unban")]
    public async Task<ApiResponse<BatchOperationResult>> BatchUnban([FromBody] BatchBanRequest request)
    {
        RequirePermission(AdminPermissions.PlayersBan);
        return ApiResponse<BatchOperationResult>.Ok(await _adminService.UnbanBatchAsync(request.UserIds));
    }

    /// <summary>批量修改玩家货币/等级/经验。</summary>
    [HttpPut("players/batch-currency")]
    public async Task<ApiResponse<BatchOperationResult>> BatchUpdateCurrency([FromBody] BatchUpdateCurrencyRequest request)
    {
        RequirePermission(AdminPermissions.PlayersEdit);
        return ApiResponse<BatchOperationResult>.Ok(await _adminService.UpdateCurrencyBatchAsync(request));
    }
}
