using Microsoft.AspNetCore.Mvc;
using WlidCourt.Api.Models;

namespace WlidCourt.Api.Controllers;

/// <summary>
/// 小游戏：上下限校验、验分（防作弊二次验分）（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/minigame")]
public class MiniGameController : ControllerBase
{
    /// <summary>开始小游戏</summary>
    [HttpPost("start/{gameId}")]
    public Task<ApiResponse<object>> Start(string gameId)
    {
        throw new NotImplementedException();
    }

    /// <summary>结束并验分（二次验分）</summary>
    [HttpPost("finish")]
    public Task<ApiResponse<object>> Finish([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>每日上限</summary>
    [HttpGet("daily-cap")]
    public Task<ApiResponse<object>> GetDailyCap()
    {
        throw new NotImplementedException();
    }
}
