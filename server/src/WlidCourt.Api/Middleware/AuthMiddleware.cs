using WlidCourt.Api.Models;
using WlidCourt.Api.Utils;

namespace WlidCourt.Api.Middleware;

/// <summary>
/// 鉴权中间件：解析 token → 校验会话（SessionStore，Redis 就绪前为内存实现）→ 注入用户上下文。
/// login / register 免鉴权；其余接口未认证返回 { code:401 }（文档 02 §2.6）。
/// </summary>
public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SessionStore _sessions;

    public AuthMiddleware(RequestDelegate next, SessionStore sessions)
    {
        _next = next;
        _sessions = sessions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.EndsWith("/user/login", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/user/register", StringComparison.OrdinalIgnoreCase) ||
            path == "/" || path == "/favicon.ico")
        {
            await _next(context);
            return;
        }

        var auth = context.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = auth["Bearer ".Length..].Trim();
            if (_sessions.TryGetUserId(token, out var userId))
            {
                context.Items["UserId"] = userId;
                await _next(context);
                return;
            }
        }

        await WriteEnvelopeAsync(context, ApiCodes.Unauthorized, "未登录或登录已过期");
    }

    private static async Task WriteEnvelopeAsync(HttpContext context, int code, string message)
    {
        context.Response.StatusCode = StatusCodes.Status200OK; // 业务信封统一 HTTP 200
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(code, message));
    }
}
