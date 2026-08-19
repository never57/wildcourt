using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Api.Utils;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Middleware;

/// <summary>
/// 运营后台鉴权中间件：拦截 /api/v1/admin/ 前缀（放行 auth/login 与 auth/captcha）。
/// 校验 AdminTokenService 签发的令牌，并注入 context.Items["AdminId"] / ["AdminRole"] / ["AdminPermissions"]。
/// 需注册在 AuthMiddleware（游戏会话鉴权）之前，并让 AuthMiddleware 放行 admin 路径。
/// </summary>
public class AdminAuthMiddleware
{
    private const string PathPrefix = "/api/v1/admin";

    // 无需登录即可访问的路径（登录 / 验证码）
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/auth/login",
        "/auth/captcha",
        "/auth/verify-totp",
        "/auth/service-login",
    };

    private readonly RequestDelegate _next;
    private readonly AdminTokenService _tokens;

    public AdminAuthMiddleware(RequestDelegate next, AdminTokenService tokens)
    {
        _next = next;
        _tokens = tokens;
    }

    public async Task InvokeAsync(HttpContext context, WildCourtDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isAdminPath = path.StartsWith(PathPrefix, StringComparison.OrdinalIgnoreCase);

        if (isAdminPath && !IsPublicPath(path))
        {
            var auth = context.Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth["Bearer ".Length..].Trim()
                : string.Empty;
            var adminId = _tokens.TryValidate(token);
            if (adminId is null)
            {
                await WriteUnauthorizedAsync(context, "未登录或登录已过期");
                return;
            }

            // 加载管理员角色与权限（每次请求查库，保证角色变更/停用即时生效）
            var admin = await db.Admins
                .Where(x => x.AdminId == adminId.Value)
                .Select(x => new { x.IsActive, x.Role })
                .FirstOrDefaultAsync();

            if (admin is null || !admin.IsActive)
            {
                await WriteUnauthorizedAsync(context, "账号不存在或已停用");
                return;
            }

            context.Items["AdminId"] = adminId.Value;
            context.Items["AdminRole"] = admin.Role;
            context.Items["AdminPermissions"] = AdminPermissions.GetPermissions(admin.Role);
        }

        await _next(context);
    }

    private bool IsPublicPath(string path)
    {
        foreach (var item in PublicPaths)
        {
            if (path.EndsWith(item, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(ApiCodes.Unauthorized, message));
    }
}
