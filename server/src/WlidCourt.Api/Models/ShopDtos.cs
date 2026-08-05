namespace WlidCourt.Api.Models;

/// <summary>购买请求（POST /api/v1/shop/buy，文档 02 §2.2 / 09）。</summary>
public class BuyRequest
{
    public string ItemId { get; set; } = string.Empty;

    public int Count { get; set; } = 1;

    /// <summary>货币：shell（庭贝）/ coupon（邮券）。阶段 B 装饰仅支持庭贝。</summary>
    public string Currency { get; set; } = "shell";
}

/// <summary>购买响应（返回最新余额与本次购买）。</summary>
public class BuyResponse
{
    public int Shell { get; set; }

    public int Coupon { get; set; }

    public string ItemId { get; set; } = string.Empty;

    public int Count { get; set; }
}

/// <summary>货架商品（Sheet9~12 Shop；阶段 B 只实现装饰货架）。</summary>
public class ShelfItem
{
    public string Id { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int BuyPrice { get; set; }

    public int SellPrice { get; set; }

    public string UnlockCond { get; set; } = string.Empty;

    public string Effect { get; set; } = string.Empty;
}
