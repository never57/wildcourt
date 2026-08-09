namespace WildCourt.Api.Models;

/// <summary>
/// 背包条目模型（对应 inventories 表）。
/// </summary>
public class Inventory
{
    public long UserId { get; set; }

    public string ItemId { get; set; } = string.Empty;

    public int Count { get; set; }
}
