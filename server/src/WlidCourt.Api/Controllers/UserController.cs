using Microsoft.AspNetCore.Mvc;
using WlidCourt.Api.Models;

namespace WlidCourt.Api.Controllers;

/// <summary>
/// 用户：注册/登录、玩家信息、省份城市身份（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/user")]
public class UserController : ControllerBase
{
    /// <summary>注册（选省/市）</summary>
    [HttpPost("register")]
    public Task<ApiResponse<User>> Register([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>登录</summary>
    [HttpPost("login")]
    public Task<ApiResponse<string>> Login([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>玩家信息</summary>
    [HttpGet("profile")]
    public Task<ApiResponse<User>> GetProfile()
    {
        throw new NotImplementedException();
    }

    /// <summary>玩家等级</summary>
    [HttpGet("level")]
    public Task<ApiResponse<int>> GetLevel()
    {
        throw new NotImplementedException();
    }
}
