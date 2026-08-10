namespace WildCourt.Data.Entities;

/// <summary>
/// 偷菜记录表（表名 steal_records）：用于每日限额统计
/// （每好友每作物每天 1 次、偷者每天总计 20 次，文档 08 §8.3）。
/// </summary>
public class StealRecordEntity
{
    public long RecordId { get; set; }

    /// <summary>偷菜者。</summary>
    public long StealerUserId { get; set; }

    /// <summary>被偷玩家。</summary>
    public long TargetUserId { get; set; }

    public string CropId { get; set; } = string.Empty;

    public int CellId { get; set; }

    public int Count { get; set; }

    public DateTime StolenAt { get; set; }
}
