using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 每日签到（文档 02 §2.2、11 §11.6）：今日状态查询 + 周/月签到领取。
/// 路径：/api/v1/signin（对齐客户端 NetConst.SignInToday / SignInClaimWeek / SignInClaimMonth）。
/// </summary>
[ApiController]
[Route("api/v1/signin")]
public class DailySignInController : ControllerBase
{
    private readonly SignInService _signInService;

    public DailySignInController(SignInService signInService)
    {
        _signInService = signInService;
    }

    /// <summary>今日签到状态（signedToday + 连续签到天数 days）。</summary>
    [HttpGet("today")]
    public async Task<ApiResponse<SignInTodayResponse>> GetToday()
    {
        var result = await _signInService.GetTodayAsync(GetUserId());
        return ApiResponse<SignInTodayResponse>.Ok(result);
    }

    /// <summary>领取周签到（执行今日签到 + 发放当日 7 天循环奖励，幂等防重复）。</summary>
    [HttpPost("claim-week")]
    public async Task<ApiResponse<SignInClaimResponse>> ClaimWeek()
    {
        var result = await _signInService.ClaimWeekAsync(GetUserId());
        return ApiResponse<SignInClaimResponse>.Ok(result);
    }

    /// <summary>领取月签到（按累计天数领取全部已达成里程碑，每档限领一次）。</summary>
    [HttpPost("claim-month")]
    public async Task<ApiResponse<SignInClaimResponse>> ClaimMonth()
    {
        var result = await _signInService.ClaimMonthAsync(GetUserId());
        return ApiResponse<SignInClaimResponse>.Ok(result);
    }

    private long GetUserId()
    {
        if (HttpContext.Items.TryGetValue("UserId", out var value) && value is long userId)
            return userId;
        throw new BusinessException(ApiCodes.Unauthorized, "未登录");
    }
}
