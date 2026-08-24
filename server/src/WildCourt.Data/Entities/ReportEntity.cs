namespace WildCourt.Data.Entities;

/// <summary>
/// 举报表（表名 reports）。
/// 玩家 A 举报玩家 B 后生成一条记录；管理平台审理后可封禁被举报人聊天。
/// </summary>
public class ReportEntity
{
    public long Id { get; set; }

    /// <summary>举报人 userId（A）。</summary>
    public long ReporterUserId { get; set; }

    /// <summary>被举报人 userId（B）。</summary>
    public long ReportedUserId { get; set; }

    /// <summary>举报理由。</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>状态：0=待审理，1=已处理。</summary>
    public int Status { get; set; } = 0;

    /// <summary>处理管理员（可选）。</summary>
    public int? HandledByAdminId { get; set; }

    /// <summary>处理时间（可选）。</summary>
    public DateTime? HandledAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
