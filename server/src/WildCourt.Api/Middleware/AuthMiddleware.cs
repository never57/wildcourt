using WildCourt.Api.Models;
using WildCourt.Api.Utils;

namespace WildCourt.Api.Middleware;

/// <summary>
/// 鉴权中间件：解析 token（Authorization: Bearer 或 SignalR ?access_token=）→ 校验会话
/// （Redis user_session:{token}）→ 注入 HttpContext.Items["UserId"]。
/// 放行：login / register / 根路径 / SignalR Hub（/hub/，ChatHub 内自行校验令牌）。
/// 未认证统一返回 HTTP 200 + 业务信封 { code:401 }（客户端按 envelope.Code 判断）。
/// </summary>
public class AuthMiddleware
{
    /// <summary>实时在线滑动窗口：TTL 内有任意请求即视为在线，无请求则视为离线。</summary>
    private static readonly TimeSpan OnlineTtl = TimeSpan.FromMinutes(5);

    private readonly RequestDelegate _next;
    private readonly ISessionStore _sessions;
    private readonly RedisUtil _redis;

    public AuthMiddleware(RequestDelegate next, ISessionStore sessions, RedisUtil redis)
    {
        _next = next;
        _sessions = sessions;
        _redis = redis;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        // 放行：登录/注册、玩家端公告（登录前展示）、根路径、favicon、健康检查、SignalR Hub（negotiate 握手 + WebSocket 升级）、
        // 运营后台（/api/v1/admin/*，由 AdminAuthMiddleware 独立鉴权，需注册在本中间件之前）
        if (path.EndsWith("/user/login", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/user/register", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/announcements", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hub/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/admin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var token = ExtractToken(context);
        if (!string.IsNullOrEmpty(token))
        {
            var userId = await _sessions.TryGetUserIdAsync(token);
            if (userId.HasValue)
            {
                context.Items["UserId"] = userId.Value;
                context.Items["Token"] = token;
                // 刷新实时在线标记（滑动窗口，管理后台"实时在线"按此统计）
                await _redis.TouchOnlineAsync(userId.Value, OnlineTtl);
                await _next(context);
                return;
            }
        }

        await WriteEnvelopeAsync(context, ApiCodes.Unauthorized, "未登录或登录已过期");
    }

    /// <summary>
    /// 提取 token：优先 Authorization: Bearer；SignalR 惯例为查询参数 ?access_token=。
    /// </summary>
    private static string? ExtractToken(HttpContext context)
    {
        var auth = context.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return auth["Bearer ".Length..].Trim();
        }
        var query = context.Request.Query["access_token"].ToString();
        return string.IsNullOrEmpty(query) ? null : query;
    }

    private static async Task WriteEnvelopeAsync(HttpContext context, int code, string message)
    {
        context.Response.StatusCode = StatusCodes.Status200OK; // 业务信封统一 HTTP 200
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(code, message));
    }
}
