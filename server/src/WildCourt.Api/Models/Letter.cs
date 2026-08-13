namespace WildCourt.Api.Models;

/// <summary>
/// 信件模型（对应 letters 表，字段契约与客户端 LetterItem 对齐，camelCase 输出）。
/// </summary>
public class Letter
{
    public long LetterId { get; set; }

    /// <summary>寄件人用户 ID（0 = 官方/系统）。</summary>
    public long FromUserId { get; set; }

    /// <summary>寄件人昵称（官方信为“官方”）。</summary>
    public string FromNickname { get; set; } = string.Empty;

    /// <summary>收件人用户 ID。</summary>
    public long ToUserId { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? ItemId { get; set; }

    /// <summary>附件类型：shell（庭贝）/ coupon（邮券）/ item（背包道具/非遗）；空 = 无附件。</summary>
    public string? RewardType { get; set; }

    /// <summary>附件数量（shell/coupon 为货币数额，item 为背包数量）。</summary>
    public int RewardCount { get; set; }

    public string Status { get; set; } = string.Empty;

    /// <summary>创建时间（毫秒时间戳，对齐客户端 long createdAt）。</summary>
    public long CreatedAt { get; set; }
}

/// <summary>收件箱响应（与客户端 InboxResponse 对齐）。</summary>
public class InboxResponse
{
    public List<Letter> Letters { get; set; } = new();
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
