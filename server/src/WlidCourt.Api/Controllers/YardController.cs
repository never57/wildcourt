using Microsoft.AspNetCore.Mvc;
using WlidCourt.Api.Models;

namespace WlidCourt.Api.Controllers;

/// <summary>
/// 庭院：布局、容量、互访（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/yard")]
public class YardController : ControllerBase
{
    /// <summary>我的庭院</summary>
    [HttpGet]
    public Task<ApiResponse<Yard>> GetYard()
    {
        throw new NotImplementedException();
    }

    /// <summary>保存布局（3 个布局槽）</summary>
    [HttpPut("layout")]
    public Task<ApiResponse<object>> SaveLayout([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>互访庭院</summary>
    [HttpGet("visit/{userId}")]
    public Task<ApiResponse<Yard>> Visit(long userId)
    {
        throw new NotImplementedException();
    }
}
