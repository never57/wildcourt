using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 好友：申请、接受、偷菜、喂食、互访庭院（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/friend")]
public class FriendController : ControllerBase
{
    /// <summary>好友申请</summary>
    [HttpPost("apply")]
    public Task<ApiResponse<object>> Apply([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>接受申请</summary>
    [HttpPost("accept")]
    public Task<ApiResponse<object>> Accept([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>偷菜（30% 产量、每日上限）</summary>
    [HttpPost("steal")]
    public Task<ApiResponse<object>> Steal([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>喂食（3 次/天）</summary>
    [HttpPost("feed")]
    public Task<ApiResponse<object>> Feed([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>互访庭院</summary>
    [HttpGet("yard/{userId}")]
    public Task<ApiResponse<Yard>> GetYard(long userId)
    {
        throw new NotImplementedException();
    }
}
