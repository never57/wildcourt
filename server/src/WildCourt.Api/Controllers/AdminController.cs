using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 运营后台：信件人工审核、封禁（文档 02 §2.2）。
/// 玩家封禁细节（踢会话等）见 <see cref="AdminService"/>，本控制器保留原始路由兼容。
/// </summary>
public class AdminController : AdminControllerBase
{
    private readonly AdminService _adminService;

    public AdminController(AdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>信件人工审核：通过/退回。</summary>
    [HttpPost("letter/review")]
    public async Task<ApiResponse<object>> ReviewLetter([FromBody] ReviewLetterRequest request)
    {
        RequirePermission(AdminPermissions.ReviewsEdit);
        await _adminService.ReviewLetterAsync(request.LetterId, request.Approve);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>审核队列：待审核信件列表。</summary>
    [HttpGet("audit-queue")]
    public async Task<ApiResponse<List<ReviewQueueItemDto>>> GetAuditQueue()
    {
        RequirePermission(AdminPermissions.ReviewsView);
        return ApiResponse<List<ReviewQueueItemDto>>.Ok(await _adminService.GetAuditQueueAsync());
    }

    /// <summary>封禁用户（原始路由：POST /api/v1/admin/ban/{userId}）。</summary>
    [HttpPost("ban/{userId}")]
    public async Task<ApiResponse<object>> BanUser(long userId)
    {
        RequirePermission(AdminPermissions.PlayersBan);
        var changed = await _adminService.BanAsync(userId);
        return ApiResponse<object>.Ok(new { Changed = changed });
    }
}
