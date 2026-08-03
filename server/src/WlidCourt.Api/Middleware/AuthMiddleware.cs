namespace WlidCourt.Api.Middleware;

/// <summary>
/// 鉴权中间件：解析 token → 校验 user_session:{token} → 注入用户上下文；Admin 接口校验运营角色（文档 02 §2.6）。
/// </summary>
public class AuthMiddleware
{
    private readonly RequestDelegate _next;

    public AuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // TODO: 解析 token 并校验会话；跳过登录/注册端点。
        await _next(context);
    }
}
