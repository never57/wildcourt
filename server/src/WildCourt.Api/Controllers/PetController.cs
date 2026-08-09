using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 宠物：信息、派遣（10 步校验）、归家（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/pet")]
public class PetController : ControllerBase
{
    /// <summary>宠物信息</summary>
    [HttpGet("info")]
    public Task<ApiResponse<Pet>> GetInfo()
    {
        throw new NotImplementedException();
    }

    /// <summary>派遣（10 步校验）</summary>
    [HttpPost("dispatch")]
    public Task<ApiResponse<object>> Dispatch([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>召回归家</summary>
    [HttpPost("recall")]
    public Task<ApiResponse<object>> Recall([FromBody] object request)
    {
        throw new NotImplementedException();
    }
}
