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

    /// <summary>官方发信：向指定玩家送达一封官方信件（可携带附件）。</summary>
    [HttpPost("letters")]
    public async Task<ApiResponse<object>> SendOfficialLetter([FromBody] OfficialLetterRequest request)
    {
        RequirePermission(AdminPermissions.AnnouncementsEdit);
        var letterId = await _adminService.SendOfficialLetterAsync(request);
        return ApiResponse<object>.Ok(new { LetterId = letterId });
    }

    /// <summary>批量发信：向多个玩家发送同一封官方信件。</summary>
    [HttpPost("letters/batch")]
    public async Task<ApiResponse<BatchOperationResult>> SendOfficialLetterBatch([FromBody] OfficialLetterBatchRequest request)
    {
        RequirePermission(AdminPermissions.AnnouncementsEdit);
        return ApiResponse<BatchOperationResult>.Ok(await _adminService.SendOfficialLetterBatchAsync(request));
    }

    /// <summary>按条件群发：按筛选条件匹配玩家后群发官方信件。</summary>
    [HttpPost("letters/broadcast")]
    public async Task<ApiResponse<BatchOperationResult>> BroadcastOfficialLetter([FromBody] OfficialLetterBroadcastRequest request)
    {
        RequirePermission(AdminPermissions.AnnouncementsEdit);
        return ApiResponse<BatchOperationResult>.Ok(await _adminService.BroadcastOfficialLetterAsync(request));
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
