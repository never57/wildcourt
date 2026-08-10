using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 每日签到（复用结算链路，仅读写接口）（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/signin")]
public class DailySignInController : ControllerBase
{
    /// <summary>今日签到状态</summary>
    [HttpGet("today")]
    public Task<ApiResponse<object>> GetToday()
    {
        throw new NotImplementedException();
    }

    /// <summary>领取周签到</summary>
    [HttpPost("claim-week")]
    public Task<ApiResponse<object>> ClaimWeek()
    {
        throw new NotImplementedException();
    }

    /// <summary>领取月签到</summary>
    [HttpPost("claim-month")]
    public Task<ApiResponse<object>> ClaimMonth()
    {
        throw new NotImplementedException();
    }
}
