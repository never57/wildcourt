using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 运营后台控制器基类：统一路由前缀 /api/v1/admin，提供当前管理员 ID/角色/权限解析。
/// 鉴权由 AdminAuthMiddleware 完成（context.Items["AdminId"] / ["AdminRole"] / ["AdminPermissions"]）。
/// </summary>
[ApiController]
[Route("api/v1/admin")]
public abstract class AdminControllerBase : ControllerBase
{
    protected long GetAdminId()
    {
        if (HttpContext.Items.TryGetValue("AdminId", out var value) && value is long adminId)
        {
            return adminId;
        }
        throw new BusinessException(ApiCodes.Unauthorized, "未登录");
    }

    /// <summary>当前管理员角色。</summary>
    protected string GetAdminRole() =>
        HttpContext.Items.TryGetValue("AdminRole", out var role) && role is string r ? r : string.Empty;

    /// <summary>当前管理员权限集合。</summary>
    protected HashSet<string> GetAdminPermissions() =>
        HttpContext.Items.TryGetValue("AdminPermissions", out var perms) && perms is HashSet<string> p
            ? p
            : new HashSet<string>();

    /// <summary>
    /// 校验当前管理员是否拥有指定权限（任一满足即通过）。
    /// 无权限抛 403 业务异常，由 ExceptionMiddleware 统一转 JSON。
    /// </summary>
    protected void RequirePermission(params string[] permissions)
    {
        if (permissions.Length == 0) return;
        var has = GetAdminPermissions();
        if (!permissions.Any(has.Contains))
        {
            throw new BusinessException(ApiCodes.Forbidden,
                $"无权限执行该操作，需要权限：{string.Join(" / ", permissions)}");
        }
    }
}
