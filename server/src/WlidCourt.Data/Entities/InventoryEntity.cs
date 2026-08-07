namespace WlidCourt.Data.Entities;

/// <summary>
/// 背包表（表名 inventories）。
/// 对应文档 15.3：user_id, item_id, count。
/// </summary>
public class InventoryEntity
{
    public long UserId { get; set; }

    public string ItemId { get; set; } = string.Empty;

    public int Count { get; set; }
}
