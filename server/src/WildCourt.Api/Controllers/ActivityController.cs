using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 活动：列表（Lua 配置）、领取（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/activity")]
public class ActivityController : ControllerBase
{
    /// <summary>活动列表</summary>
    [HttpGet("list")]
    public Task<ApiResponse<List<object>>> GetList()
    {
        throw new NotImplementedException();
    }

    /// <summary>领取活动奖励</summary>
    [HttpPost("claim/{activityId}")]
    public Task<ApiResponse<object>> Claim(string activityId)
    {
        throw new NotImplementedException();
    }
}
