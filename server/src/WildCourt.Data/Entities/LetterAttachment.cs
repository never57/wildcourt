namespace WildCourt.Data.Entities;

/// <summary>
/// 邮件附件项（多附件，序列化进 letters.attachments JSON 列）。
/// 类型不可重复：shell / coupon 各至多一个，item 可按不同 ItemId 多个。
/// </summary>
public class LetterAttachment
{
    /// <summary>附件类型：shell（庭贝）/ coupon（邮券）/ item（背包道具/非遗）。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>道具/非遗 ID（type=item 时有值）。</summary>
    public string? ItemId { get; set; }

    /// <summary>数量（shell/coupon 为货币数额，item 为背包数量）。</summary>
    public int Count { get; set; }
}
