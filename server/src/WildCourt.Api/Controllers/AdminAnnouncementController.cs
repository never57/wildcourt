using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>游戏公告管理：增删改查 + 发布。</summary>
public class AdminAnnouncementController : AdminControllerBase
{
    private readonly AdminService _adminService;

    public AdminAnnouncementController(AdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("announcements")]
    public async Task<ApiResponse<List<AnnouncementDto>>> List()
    {
        RequirePermission(AdminPermissions.AnnouncementsView);
        return ApiResponse<List<AnnouncementDto>>.Ok(await _adminService.ListAnnouncementsAsync());
    }

    [HttpPost("announcements")]
    public async Task<ApiResponse<object>> Create([FromBody] AnnouncementRequest request)
    {
        RequirePermission(AdminPermissions.AnnouncementsEdit);
        var id = await _adminService.CreateAnnouncementAsync(GetAdminId(), request);
        return ApiResponse<object>.Ok(new { AnnouncementId = id });
    }

    [HttpPut("announcements/{id}")]
    public async Task<ApiResponse<object>> Update(long id, [FromBody] AnnouncementRequest request)
    {
        RequirePermission(AdminPermissions.AnnouncementsEdit);
        await _adminService.UpdateAnnouncementAsync(id, request);
        return ApiResponse<object>.Ok(new object());
    }

    [HttpDelete("announcements/{id}")]
    public async Task<ApiResponse<object>> Delete(long id)
    {
        RequirePermission(AdminPermissions.AnnouncementsEdit);
        await _adminService.DeleteAnnouncementAsync(id);
        return ApiResponse<object>.Ok(new object());
    }

    [HttpPost("announcements/{id}/publish")]
    public async Task<ApiResponse<object>> Publish(long id)
    {
        RequirePermission(AdminPermissions.AnnouncementsEdit);
        await _adminService.PublishAnnouncementAsync(id);
        return ApiResponse<object>.Ok(new object());
    }
}
