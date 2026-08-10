using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 信件：写信、收信、回赠、审核状态（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/letter")]
public class LetterController : ControllerBase
{
    /// <summary>写信（50 字 + 随附非遗）</summary>
    [HttpPost("send")]
    public Task<ApiResponse<long>> Send([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>收件箱</summary>
    [HttpGet("inbox")]
    public Task<ApiResponse<List<Letter>>> GetInbox()
    {
        throw new NotImplementedException();
    }

    /// <summary>回赠</summary>
    [HttpPost("gift-back")]
    public Task<ApiResponse<object>> GiftBack([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>审核状态</summary>
    [HttpGet("status/{letterId}")]
    public Task<ApiResponse<Letter>> GetStatus(long letterId)
    {
        throw new NotImplementedException();
    }
}
