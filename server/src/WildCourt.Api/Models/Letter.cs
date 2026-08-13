namespace WildCourt.Api.Models;

/// <summary>
/// 信件模型（对应 letters 表）。
/// </summary>
public class Letter
{
    public long LetterId { get; set; }

    public long FromUser { get; set; }

    public long ToUser { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? ItemId { get; set; }

    /// <summary>附件类型：shell（庭贝）/ coupon（邮券）/ item（背包道具/非遗）；空 = 无附件。</summary>
    public string? RewardType { get; set; }

    /// <summary>附件数量（shell/coupon 为货币数额，item 为背包数量）。</summary>
    public int RewardCount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

/// <summary>领取邮件附件请求（与客户端 ClaimLetterRequest 字段对齐）。</summary>
public class ClaimLetterRequest
{
    public long LetterId { get; set; }
}

/// <summary>领取邮件附件结果（与客户端 ClaimResponse 字段对齐）。</summary>
public class ClaimResponse
{
    public long LetterId { get; set; }

    /// <summary>附件类型：shell / coupon / item；空 = 无附件。</summary>
    public string RewardType { get; set; } = string.Empty;

    /// <summary>领取到的道具/非遗 ID（reward_type=item 时有值）。</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>领取数量（shell/coupon 为货币数额，item 为背包数量）。</summary>
    public int Count { get; set; }
}
