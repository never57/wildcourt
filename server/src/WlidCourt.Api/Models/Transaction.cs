namespace WlidCourt.Api.Models;

/// <summary>
/// 交易记录模型（对应 transactions 表）。
/// </summary>
public class Transaction
{
    public long TransId { get; set; }

    public long UserId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;

    public int Count { get; set; }

    public int Price { get; set; }
}
