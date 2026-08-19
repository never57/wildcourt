using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 运营后台认证与管理员账号管理。
/// 登录/验证码放行（AdminAuthMiddleware 白名单），其余需 Bearer 运营令牌。
/// </summary>
public class AdminAuthController : AdminControllerBase
{
    private readonly AdminService _adminService;

    public AdminAuthController(AdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>图形验证码（登录前获取）。</summary>
    [HttpGet("auth/captcha")]
    public async Task<ApiResponse<CaptchaResponse>> Captcha()
    {
        return ApiResponse<CaptchaResponse>.Ok(await _adminService.GetCaptchaAsync());
    }

    /// <summary>管理员登录（密码为客户端 SHA256 hex，带验证码；启用双因素需两步）。</summary>
    [HttpPost("auth/login")]
    public async Task<ApiResponse<AdminLoginResponse>> Login([FromBody] AdminLoginRequest request)
    {
        var result = await _adminService.LoginAsync(
            request.Username,
            request.Password,
            request.CaptchaId,
            request.CaptchaCode,
            request.TotpCode,
            request.TotpTicket);
        return ApiResponse<AdminLoginResponse>.Ok(result);
    }

    /// <summary>服务间登录（MCP / 部署脚本等受信任内部服务，校验共享密钥绕过验证码）。</summary>
    [HttpPost("auth/service-login")]
    public async Task<ApiResponse<AdminLoginResponse>> ServiceLogin([FromBody] ServiceLoginRequest request)
    {
        var result = await _adminService.ServiceLoginAsync(request.Username, request.ServiceKey);
        return ApiResponse<AdminLoginResponse>.Ok(result);
    }

    /// <summary>当前管理员信息（含角色与权限集合）。</summary>
    [HttpGet("auth/profile")]
    public async Task<ApiResponse<AdminProfileResponse>> Profile()
    {
        return ApiResponse<AdminProfileResponse>.Ok(await _adminService.GetProfileAsync(GetAdminId()));
    }

    /// <summary>修改密码（新密码为客户端 SHA256 hex）。</summary>
    [HttpPost("auth/change-password")]
    public async Task<ApiResponse<object>> ChangePassword([FromBody] ChangeAdminPasswordRequest request)
    {
        await _adminService.ChangePasswordAsync(GetAdminId(), request.OldPassword, request.NewPassword);
        return ApiResponse<object>.Ok(new object());
    }

    // ============ 双因素（TOTP） ============

    /// <summary>生成双因素绑定密钥（未启用时调用）。</summary>
    [HttpPost("auth/2fa/setup")]
    public async Task<ApiResponse<TwoFactorSetupResponse>> SetupTwoFactor()
    {
        return ApiResponse<TwoFactorSetupResponse>.Ok(await _adminService.SetupTwoFactorAsync(GetAdminId()));
    }

    /// <summary>确认并启用双因素。</summary>
    [HttpPost("auth/2fa/enable")]
    public async Task<ApiResponse<object>> EnableTwoFactor([FromBody] EnableTwoFactorRequest request)
    {
        await _adminService.EnableTwoFactorAsync(GetAdminId(), request.Secret, request.Code);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>关闭双因素（需校验当前动态码）。</summary>
    [HttpPost("auth/2fa/disable")]
    public async Task<ApiResponse<object>> DisableTwoFactor([FromBody] DisableTwoFactorRequest request)
    {
        await _adminService.DisableTwoFactorAsync(GetAdminId(), request.Code);
        return ApiResponse<object>.Ok(new object());
    }

    // ============ 角色 ============

    /// <summary>角色定义列表（含权限集合）。</summary>
    [HttpGet("roles")]
    public ApiResponse<List<AdminRoleInfoDto>> Roles()
    {
        RequirePermission(AdminPermissions.AdminsView);
        return ApiResponse<List<AdminRoleInfoDto>>.Ok(_adminService.ListRoles());
    }

    // ============ 管理员管理 ============

    /// <summary>管理员列表。</summary>
    [HttpGet("admins")]
    public async Task<ApiResponse<List<AdminItemDto>>> ListAdmins()
    {
        RequirePermission(AdminPermissions.AdminsView);
        return ApiResponse<List<AdminItemDto>>.Ok(await _adminService.ListAdminsAsync());
    }

    /// <summary>新建管理员。</summary>
    [HttpPost("admins")]
    public async Task<ApiResponse<object>> CreateAdmin([FromBody] CreateAdminRequest request)
    {
        RequirePermission(AdminPermissions.AdminsEdit);
        await _adminService.CreateAdminAsync(request.Username, request.Password, request.DisplayName, request.Role);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>启用/停用管理员。</summary>
    [HttpPost("admins/{adminId}/active/{active}")]
    public async Task<ApiResponse<object>> SetActive(long adminId, bool active)
    {
        RequirePermission(AdminPermissions.AdminsEdit);
        await _adminService.SetAdminActiveAsync(adminId, active);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>修改管理员角色。</summary>
    [HttpPut("admins/{adminId}/role")]
    public async Task<ApiResponse<object>> UpdateRole(long adminId, [FromBody] UpdateAdminRoleRequest request)
    {
        RequirePermission(AdminPermissions.AdminsEdit);
        await _adminService.UpdateAdminRoleAsync(adminId, request.Role, GetAdminId());
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>重置指定管理员密码（管理员操作，新密码为客户端 SHA256 hex）。</summary>
    [HttpPost("admins/{adminId}/reset-password")]
    public async Task<ApiResponse<object>> ResetPassword(long adminId, [FromBody] ResetAdminPasswordRequest request)
    {
        RequirePermission(AdminPermissions.AdminsEdit);
        await _adminService.ResetAdminPasswordAsync(adminId, request.NewPassword);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>删除管理员（不能删除自己，且至少保留一个启用账号）。</summary>
    [HttpDelete("admins/{adminId}")]
    public async Task<ApiResponse<object>> DeleteAdmin(long adminId)
    {
        RequirePermission(AdminPermissions.AdminsEdit);
        await _adminService.DeleteAdminAsync(adminId, GetAdminId());
        return ApiResponse<object>.Ok(new object());
    }
}
