using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Data.DbContext;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 玩家端公告：公开查询接口，免登录（AuthMiddleware 放行）。
/// 仅返回已发布（published）公告，按 ID 倒序。
/// </summary>
[ApiController]
[Route("api/v1/announcements")]
public class AnnouncementController : ControllerBase
{
    private readonly WildCourtDbContext _db;

    public AnnouncementController(WildCourtDbContext db)
    {
        _db = db;
    }

    /// <summary>已发布公告列表（玩家登录前展示用）。</summary>
    [HttpGet("")]
    public async Task<ApiResponse<List<AnnouncementDto>>> List()
    {
        var list = await _db.Announcements
            .Where(a => a.Status == "published")
            .OrderByDescending(a => a.AnnouncementId)
            .Select(a => new AnnouncementDto
            {
                AnnouncementId = a.AnnouncementId,
                Title = a.Title,
                Content = a.Content,
                Status = a.Status,
                CreatorName = a.CreatedBy.ToString(),
                CreatedAt = a.CreatedAt,
                PublishedAt = a.PublishedAt,
            })
            .ToListAsync();
        return ApiResponse<List<AnnouncementDto>>.Ok(list);
    }
}
