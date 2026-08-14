using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 信箱（仅官方信息发放链路，文档 02 §2.2）：
/// 收件箱 → 领取附件 → 删除已读。
/// 玩家间写信（send/gift-back/审核）已剥离至独立写信系统，本控制器不再提供。
/// </summary>
[ApiController]
[Route("api/v1/letter")]
public class LetterController : ControllerBase
{
    private readonly LetterService _letterService;

    public LetterController(LetterService letterService)
    {
        _letterService = letterService;
    }

    /// <summary>收件箱（官方信件，按时间倒序）。</summary>
    [HttpGet("inbox")]
    public async Task<ApiResponse<InboxResponse>> GetInbox()
    {
        var inbox = await _letterService.InboxAsync(GetUserId());
        return ApiResponse<InboxResponse>.Ok(inbox);
    }

    /// <summary>标记信件已读（玩家点击 MailItem 时调用，未读 → 已读）。</summary>
    [HttpPost("mark-read")]
    public async Task<ApiResponse<object>> MarkRead([FromBody] ClaimLetterRequest request)
    {
        await _letterService.MarkReadAsync(GetUserId(), request.LetterId);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>领取邮件随附道具/物品（itemId 附件入背包，幂等防重复）。</summary>
    [HttpPost("claim")]
    public async Task<ApiResponse<ClaimResponse>> Claim([FromBody] ClaimLetterRequest request)
    {
        var result = await _letterService.ClaimAsync(GetUserId(), request.LetterId);
        return ApiResponse<ClaimResponse>.Ok(result);
    }

    /// <summary>删除已读/已领取信件（软删除，仅本人可删）。</summary>
    [HttpDelete("{letterId}")]
    public async Task<ApiResponse<object>> Delete(long letterId)
    {
        await _letterService.DeleteAsync(GetUserId(), letterId);
        return ApiResponse<object>.Ok(new object());
    }

    private long GetUserId()
    {
        if (HttpContext.Items.TryGetValue("UserId", out var value) && value is long userId)
        {
            return userId;
        }
        throw new BusinessException(ApiCodes.Unauthorized, "未登录");
    }
}
