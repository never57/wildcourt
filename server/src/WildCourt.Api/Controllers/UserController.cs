using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 用户：注册/登录、玩家信息、省份城市身份（文档 02 §2.2）。
/// 除 register/login 外均需携带 Authorization: Bearer {token}（AuthMiddleware 注入 UserId）。
/// </summary>
[ApiController]
[Route("api/v1/user")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;
    private readonly PlayerService _playerService;

    public UserController(UserService userService, PlayerService playerService)
    {
        _userService = userService;
        _playerService = playerService;
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
        var userId = GetUserId();
        var user = await _userService.GetProfileAsync(userId);
        if (user == null)
        {
            throw new BusinessException(ApiCodes.NotFound, "用户不存在");
        }
        return ApiResponse<User>.Ok(user);
    }

    /// <summary>修改昵称（玩家自助，token 鉴权）。</summary>
    [HttpPut("nickname")]
    public async Task<ApiResponse<User>> UpdateNickname([FromBody] UpdateNicknameRequest request)
    {
        var user = await _userService.UpdateNicknameAsync(GetUserId(), request.Nickname);
        return ApiResponse<User>.Ok(user);
    }

    /// <summary>玩家等级与经验（PlayerService 权威）。</summary>
    [HttpGet("level")]
    public async Task<ApiResponse<LevelResponse>> GetLevel()
    {
        var userId = GetUserId();
        var user = await _userService.GetProfileAsync(userId);
        if (user == null)
        {
            throw new BusinessException(ApiCodes.NotFound, "用户不存在");
        }
        var level = await _playerService.GetLevelAsync(userId);
        return ApiResponse<LevelResponse>.Ok(new LevelResponse { Level = level, Exp = user.Exp });
    }

    /// <summary>联调测试接口：增加玩家经验并重算等级（alpha 阶段，后续由真实玩法来源替换或收紧）。</summary>
    [HttpPost("exp")]
    public async Task<ApiResponse<LevelResponse>> AddExp([FromBody] AddExpRequest request)
    {
        var userId = GetUserId();
        var level = await _playerService.AddExpAsync(userId, request.Exp);
        var user = await _userService.GetProfileAsync(userId);
        return ApiResponse<LevelResponse>.Ok(new LevelResponse { Level = level, Exp = user?.Exp ?? 0 });
    }

    /// <summary>背包快照（阶段 C：编辑面板“已拥有”状态）。</summary>
    [HttpGet("inventory")]
    public async Task<ApiResponse<InventoryResponse>> GetInventory()
    {
        var result = await _userService.GetInventoryAsync(GetUserId());
        return ApiResponse<InventoryResponse>.Ok(result);
    }

    private long GetUserId()
    {
        if (HttpContext.Items.TryGetValue("UserId", out var value) && value is long userId)
        {
            return userId;
        }
        throw new BusinessException(ApiCodes.Unauthorized, "未登录");
    }
}
