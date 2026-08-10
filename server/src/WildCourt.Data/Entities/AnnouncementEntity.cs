namespace WildCourt.Data.Entities;

/// <summary>
/// 游戏内公告（表名 announcements）。
/// status: draft（草稿）/ published（已发布）/ archived（已归档）。
/// </summary>
public class AnnouncementEntity
{
    public long AnnouncementId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string Status { get; set; } = "draft";

    /// <summary>创建人管理员 ID（admins.admin_id）。</summary>
    public long CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }
}
