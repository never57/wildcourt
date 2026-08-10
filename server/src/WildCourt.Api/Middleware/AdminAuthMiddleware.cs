using WildCourt.Api.Models;
using WildCourt.Api.Utils;

namespace WildCourt.Api.Middleware;

/// <summary>
/// 运营后台鉴权中间件：拦截 /api/v1/admin/ 前缀（放行 auth/login）。
/// 校验 AdminTokenService 签发的令牌并注入 context.Items["AdminId"]。
/// 需注册在 AuthMiddleware（游戏会话鉴权）之前，并让 AuthMiddleware 放行 admin 路径。
/// </summary>
public class AdminAuthMiddleware
{
    private const string PathPrefix = "/api/v1/admin";

    private readonly RequestDelegate _next;
    private readonly AdminTokenService _tokens;

    public AdminAuthMiddleware(RequestDelegate next, AdminTokenService tokens)
    {
        _next = next;
        _tokens = tokens;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isAdminPath = path.StartsWith(PathPrefix, StringComparison.OrdinalIgnoreCase);

        if (isAdminPath && !path.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            var auth = context.Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : string.Empty;
            var adminId = _tokens.TryValidate(token);
            if (adminId is null)
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(ApiCodes.Unauthorized, "未登录或登录已过期"));
                return;
            }
            context.Items["AdminId"] = adminId.Value;
        }

        await _next(context);
    }
}
