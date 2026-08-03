using Microsoft.AspNetCore.Mvc;
using WlidCourt.Api.Models;
using WlidCourt.Api.Services;

namespace WlidCourt.Api.Controllers;

/// <summary>
/// 用户：注册/登录、玩家信息、省份城市身份（文档 02 §2.2）。
/// 除 register/login 外均需携带 Authorization: Bearer {token}（AuthMiddleware 注入 UserId）。
/// </summary>
[ApiController]
[Route("api/v1/user")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    /// <summary>注册（账号+密码，密码为客户端 SHA256 哈希）。</summary>
    [HttpPost("register")]
    public async Task<ApiResponse<User>> Register([FromBody] RegisterRequest request)
    {
        var user = await _userService.RegisterAsync(request.Username, request.Password);
        return ApiResponse<User>.Ok(user);
    }

    /// <summary>登录，返回会话 token + userId。</summary>
    [HttpPost("login")]
    public async Task<ApiResponse<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _userService.LoginAsync(request.Username, request.Password);
        return ApiResponse<LoginResponse>.Ok(result);
    }

    /// <summary>玩家信息（token 鉴权）。</summary>
    [HttpGet("profile")]
    public async Task<ApiResponse<User>> GetProfile()
    {
        var userId = (long)HttpContext.Items["UserId"]!;
        var user = await _userService.GetProfileAsync(userId);
        if (user == null)
        {
            throw new BusinessException(ApiCodes.NotFound, "用户不存在");
        }
        return ApiResponse<User>.Ok(user);
    }

    /// <summary>玩家等级（当前返回用户等级，EXP 系统待 PlayerService 落实）。</summary>
    [HttpGet("level")]
    public async Task<ApiResponse<LevelResponse>> GetLevel()
    {
        var userId = (long)HttpContext.Items["UserId"]!;
        var user = await _userService.GetProfileAsync(userId);
        if (user == null)
        {
            throw new BusinessException(ApiCodes.NotFound, "用户不存在");
        }
        return ApiResponse<LevelResponse>.Ok(new LevelResponse { Level = user.Level, Exp = 0 });
    }
}
