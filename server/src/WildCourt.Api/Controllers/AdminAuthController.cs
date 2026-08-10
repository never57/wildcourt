using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 运营后台认证与管理员账号管理。
/// 登录放行（AdminAuthMiddleware 白名单），其余需 Bearer 运营令牌。
/// </summary>
public class AdminAuthController : AdminControllerBase
{
    private readonly AdminService _adminService;

    public AdminAuthController(AdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>管理员登录（密码为客户端 SHA256 hex）。</summary>
    [HttpPost("auth/login")]
    public async Task<ApiResponse<AdminLoginResponse>> Login([FromBody] AdminLoginRequest request)
    {
        var result = await _adminService.LoginAsync(request.Username, request.Password);
        return ApiResponse<AdminLoginResponse>.Ok(result);
    }

    /// <summary>当前管理员信息。</summary>
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

    /// <summary>管理员列表。</summary>
    [HttpGet("admins")]
    public async Task<ApiResponse<List<AdminItemDto>>> ListAdmins()
    {
        return ApiResponse<List<AdminItemDto>>.Ok(await _adminService.ListAdminsAsync());
    }

    /// <summary>新建管理员。</summary>
    [HttpPost("admins")]
    public async Task<ApiResponse<object>> CreateAdmin([FromBody] CreateAdminRequest request)
    {
        await _adminService.CreateAdminAsync(request.Username, request.Password, request.DisplayName);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>启用/停用管理员。</summary>
    [HttpPost("admins/{adminId}/active/{active}")]
    public async Task<ApiResponse<object>> SetActive(long adminId, bool active)
    {
        await _adminService.SetAdminActiveAsync(adminId, active);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>重置指定管理员密码（管理员操作，新密码为客户端 SHA256 hex）。</summary>
    [HttpPost("admins/{adminId}/reset-password")]
    public async Task<ApiResponse<object>> ResetPassword(long adminId, [FromBody] ResetAdminPasswordRequest request)
    {
        await _adminService.ResetAdminPasswordAsync(adminId, request.NewPassword);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>删除管理员（不能删除自己，且至少保留一个启用账号）。</summary>
    [HttpDelete("admins/{adminId}")]
    public async Task<ApiResponse<object>> DeleteAdmin(long adminId)
    {
        await _adminService.DeleteAdminAsync(adminId, GetAdminId());
        return ApiResponse<object>.Ok(new object());
    }
}
