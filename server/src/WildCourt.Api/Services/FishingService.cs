using WildCourt.Api.Models;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 钓鱼事件上报业务（文档 02 §2.2）：接收客户端 POST /api/v1/fishing/event，
/// 落库 fishing_events 供运营管理端统计钓鱼成功率/鱼饵消耗/鱼种分布。
/// 仅做事件记录，不反写玩家背包/货币（钓获写包由客户端本地背包完成，服务端只统计）。
/// </summary>
public class FishingService
{
    private readonly WildCourtDbContext _db;

    public FishingService(WildCourtDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 记录一次钓鱼结算事件。
    /// </summary>
    /// <param name="userId">上报玩家ID（鉴权解析）。</param>
    /// <param name="eventType">success / fail。</param>
    /// <param name="baitId">本次消耗鱼饵ID。</param>
    /// <param name="fishId">成功时鱼种ID，失败为 null。</param>
    /// <param name="fishName">成功时鱼名，失败为 null。</param>
    /// <param name="reason">失败原因/成功说明。</param>
    /// <param name="timestampMs">客户端事件毫秒时间戳。</param>
    public async Task RecordEventAsync(long userId, string eventType, string baitId,
        string? fishId, string? fishName, string? reason, long timestampMs)
    {
        var entity = new FishingEventEntity
        {
            UserId = userId,
            EventType = eventType,
            BaitId = baitId ?? string.Empty,
            FishId = fishId,
            FishName = fishName,
            Reason = reason,
            TimestampMs = timestampMs,
            ServerTime = DateTime.UtcNow,
        };
        _db.FishingEvents.Add(entity);
        await _db.SaveChangesAsync();
    }
}
