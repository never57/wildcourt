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
    private readonly IWebHostEnvironment _env;

    public UserController(UserService userService, PlayerService playerService, IWebHostEnvironment env)
    {
        _userService = userService;
        _playerService = playerService;
        _env = env;
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

    /// <summary>搜索玩家（只支持按 userId 精确搜索，token 鉴权；排除自己）。</summary>
    [HttpGet("search")]
    public async Task<ApiResponse<List<PlayerSearchDto>>> Search([FromQuery] string keyword)
    {
        var result = await _userService.SearchAsync(keyword, GetUserId());
        return ApiResponse<List<PlayerSearchDto>>.Ok(result);
    }

    /// <summary>获取玩家省份城市（token 鉴权）。</summary>
    [HttpGet("location")]
    public async Task<ApiResponse<LocationResponse>> GetLocation()
    {
        var location = await _userService.GetLocationAsync(GetUserId());
        if (location == null)
        {
            throw new BusinessException(ApiCodes.NotFound, "用户不存在");
        }
        return ApiResponse<LocationResponse>.Ok(location);
    }

    /// <summary>更新玩家省份城市（玩家自助，token 鉴权）。</summary>
    [HttpPut("location")]
    public async Task<ApiResponse<User>> UpdateLocation([FromBody] UpdateLocationRequest request)
    {
        var user = await _userService.UpdateLocationAsync(GetUserId(), request.ProvinceId, request.CityId);
        return ApiResponse<User>.Ok(user);
    }

    /// <summary>
    /// 上传头像（multipart/form-data，字段名 avatar；jpg/png，≤8MB，token 鉴权）。
    /// 保存到 wwwroot/uploads/avatar/{userId}.png，并回填 User.AvatarUrl。
    /// </summary>
    [HttpPut("avatar")]
    public async Task<ApiResponse<User>> UploadAvatar([FromForm] IFormFile? avatar)
    {
        var userId = GetUserId();
        if (avatar == null || avatar.Length == 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "未选择图片");
        }

        var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png"))
        {
            throw new BusinessException(ApiCodes.BadRequest, "仅支持 jpg/png 格式");
        }
        if (avatar.Length > 8 * 1024 * 1024)
        {
            throw new BusinessException(ApiCodes.BadRequest, "图片不能超过 8MB");
        }

        // 保存路径：wwwroot/uploads/avatar/{userId}{ext}
        var webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", "avatar");
        Directory.CreateDirectory(dir);

        // 清理旧头像文件（可能换了扩展名）
        foreach (var old in new[] { ".png", ".jpg", ".jpeg" })
        {
            var oldPath = Path.Combine(dir, $"{userId}{old}");
            if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
        }

        var fileName = $"{userId}{ext}";
        var fullPath = Path.Combine(dir, fileName);
        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await avatar.CopyToAsync(stream);
        }

        // 回填 AvatarUrl（相对路径，客户端拼 Base 加载）
        var avatarUrl = $"/uploads/avatar/{fileName}";
        var user = await _userService.UpdateAvatarAsync(userId, avatarUrl);
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
