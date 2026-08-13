namespace WildCourt.Data.Entities;

/// <summary>
/// 信件表（表名 letters）。
/// 对应文档 15.3：letter_id, from_user, to_user, content, item_id, status, created_at。
/// 状态机：撰写中 → 待审核 → 通过/退回 → 送达（见文档 02 §2.3 LetterService）。
/// </summary>
public class LetterEntity
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

    /// <summary>撰写中 / 待审核 / 通过 / 退回 / 送达</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
