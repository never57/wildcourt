namespace WildCourt.Api.Models;

/// <summary>注册请求（客户端 AuthService 已对 password 做 SHA256，服务端再 BCrypt 存储）。</summary>
public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

/// <summary>登录请求。</summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

/// <summary>登录响应（会话 token + 用户 ID）。</summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public long UserId { get; set; }
}

/// <summary>增加经验请求（联调测试接口 POST /api/v1/user/exp，alpha 阶段，后续由真实玩法来源替换）。</summary>
public class AddExpRequest
{
    public int Exp { get; set; }
}

/// <summary>修改昵称请求（玩家自助，PUT /api/v1/user/nickname）。</summary>
public class UpdateNicknameRequest
{
    public string Nickname { get; set; } = string.Empty;
}

/// <summary>更新省份城市请求（玩家自助，PUT /api/v1/user/location）。</summary>
public class UpdateLocationRequest
{
    /// <summary>省份 ID（国标行政区划代码，如 440000 广东）。</summary>
    public int ProvinceId { get; set; }

    /// <summary>城市 ID（国标行政区划代码，如 440100 广州）。</summary>
    public int CityId { get; set; }
}

/// <summary>玩家省份城市信息（GET /api/v1/user/location）。</summary>
public class LocationResponse
{
    public int ProvinceId { get; set; }

    public int CityId { get; set; }
}

/// <summary>搜索到的玩家卡片（GET /api/v1/user/search，仅含加好友所需字段，不含敏感数据）。</summary>
public class PlayerSearchDto
{
    public long UserId { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public int Level { get; set; }
}
