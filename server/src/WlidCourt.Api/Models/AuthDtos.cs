namespace WlidCourt.Api.Models;

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
