namespace WlidCourt.Data.Entities;

/// <summary>
/// 交易记录表（表名 transactions）。
/// 对应文档 15.3：trans_id, user_id, type, item_id, count, price。
/// </summary>
public class TransactionEntity
{
    public long TransId { get; set; }

    public long UserId { get; set; }

    /// <summary>交易类型：shop / minigame / settlement 等</summary>
    public string Type { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;

    public int Count { get; set; }

    public int Price { get; set; }
}
