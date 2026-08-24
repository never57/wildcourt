namespace WildCourt.Api.Models;

// ============ 客户端接口 DTO ============

/// <summary>发起举报请求（POST /api/v1/report）。</summary>
public class CreateReportRequest
{
    public long ReportedUserId { get; set; }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>聊天封禁状态（GET /api/v1/chat/ban-status）。</summary>
public class ChatBanStatusDto
{
    /// <summary>当前是否处于聊天封禁中（未过期）。</summary>
    public bool Banned { get; set; }

    /// <summary>封禁截止时间（UTC，未封禁为空）。</summary>
    public DateTime? BanUntil { get; set; }

    /// <summary>剩余封禁天数（向上取整，未封禁为 0）。</summary>
    public int RemainDays { get; set; }

    /// <summary>被封禁理由（可选，管理端填写）。</summary>
    public string Reason { get; set; } = string.Empty;
}

// ============ 管理平台接口 DTO ============

/// <summary>举报列表项。</summary>
public class AdminReportListItemDto
{
    public long Id { get; set; }
    public long ReporterUserId { get; set; }
    public string ReporterNickname { get; set; } = string.Empty;
    public long ReportedUserId { get; set; }
    public string ReportedNickname { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Status { get; set; }          // 0=待审理 1=已处理
    public DateTime CreatedAt { get; set; }
}

/// <summary>举报详情（含两人全部聊天记录）。</summary>
public class AdminReportDetailDto
{
    public long Id { get; set; }
    public long ReporterUserId { get; set; }
    public string ReporterNickname { get; set; } = string.Empty;
    public long ReportedUserId { get; set; }
    public string ReportedNickname { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>被举报人当前聊天封禁状态。</summary>
    public ChatBanStatusDto ChatBan { get; set; } = new();

    /// <summary>两人之间的全部聊天记录（按时间升序）。</summary>
    public List<ChatMessageDto> ChatMessages { get; set; } = new();
}

/// <summary>处理举报请求（POST /api/v1/admin/reports/{id}/resolve）。</summary>
public class ResolveReportRequest
{
    /// <summary>封禁被举报人聊天天数。0=解封/不封禁，>0=封禁 N 天。</summary>
    public int ChatBanDays { get; set; }

    /// <summary>备注（可选）。</summary>
    public string? Note { get; set; }
}

/// <summary>封禁/解封玩家聊天请求。</summary>
public class SetChatBanRequest
{
    /// <summary>封禁天数（>0 封禁，0 解封）。</summary>
    public int Days { get; set; }

    public string? Reason { get; set; }
}
