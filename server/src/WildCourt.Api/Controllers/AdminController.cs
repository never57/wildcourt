using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 运营后台：人工审核、公告、封禁（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    /// <summary>信件人工审核</summary>
    [HttpPost("letter/review")]
    public Task<ApiResponse<object>> ReviewLetter([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>审核队列</summary>
    [HttpGet("audit-queue")]
    public Task<ApiResponse<List<object>>> GetAuditQueue()
    {
        throw new NotImplementedException();
    }

    /// <summary>封禁用户</summary>
    [HttpPost("ban/{userId}")]
    public Task<ApiResponse<object>> BanUser(long userId)
    {
        throw new NotImplementedException();
    }
}
