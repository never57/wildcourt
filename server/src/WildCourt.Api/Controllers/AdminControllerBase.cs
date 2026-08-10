using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 运营后台控制器基类：统一路由前缀 /api/v1/admin，提供当前管理员 ID 解析。
/// 鉴权由 AdminAuthMiddleware 完成（context.Items["AdminId"]）。
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
}
