namespace WildCourt.Data.Entities;

/// <summary>
/// 钓鱼事件记录表（表名 fishing_events）：客户端钓鱼成功/失败结算时
/// POST /api/v1/fishing/event 上报，服务端落库供运营管理端统计。
/// 字段对齐客户端 FishingEventDto：eventType/baitId/fishId/fishName/reason/timestampMs。
/// </summary>
public class FishingEventEntity
{
    public long EventId { get; set; }

    /// <summary>上报玩家（鉴权解析）。</summary>
    public long UserId { get; set; }

    /// <summary>事件类型：success / fail。</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>本次消耗的鱼饵ID。</summary>
    public string BaitId { get; set; } = string.Empty;

    /// <summary>成功时为鱼种ID，失败为 NULL。</summary>
    public string? FishId { get; set; }

    /// <summary>成功时为鱼名，失败为 NULL。</summary>
    public string? FishName { get; set; }

    /// <summary>失败原因；成功为钓获说明。</summary>
    public string? Reason { get; set; }

    /// <summary>客户端事件发生的 Unix 毫秒时间戳。</summary>
    public long TimestampMs { get; set; }

    /// <summary>服务端接收落库时间（UTC）。</summary>
    public DateTime ServerTime { get; set; }
}
