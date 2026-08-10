namespace WildCourt.Api.Models;

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

/// <summary>出售请求（POST /api/v1/shop/sell，仅 Config_Crops 原始作物可售）。</summary>
public class SellRequest
{
    public string ItemId { get; set; } = string.Empty;

    public int Count { get; set; } = 1;
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

/// <summary>
/// 商店配置行（Sheet9~12 Shop / Config_Shop.json，文档 16 §16.2）。
/// category=seed 的条目为种子，通过 CropId 关联 Config_Crops.json 的作物。
/// </summary>
public class ShopItemConfigRow
{
    public string Id { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int BuyPrice { get; set; }

    public int SellPrice { get; set; }

    public string UnlockCond { get; set; } = string.Empty;

    public string Effect { get; set; } = string.Empty;

    /// <summary>种子对应的作物 ID（仅 category=seed 使用，对应 Config_Crops.json）。</summary>
    public string CropId { get; set; } = string.Empty;
}
