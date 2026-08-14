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

    /// <summary>信件标题。</summary>
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>附件列表（多附件）。空 = 无附件。</summary>
    public List<LetterAttachmentDto> Attachments { get; set; } = new();

    public string Status { get; set; } = string.Empty;

    /// <summary>创建时间（毫秒时间戳，对齐客户端 long createdAt）。</summary>
    public long CreatedAt { get; set; }
}

/// <summary>附件项（响应契约，camelCase 输出，与客户端 LetterItem.attachments 元素对齐）。</summary>
public class LetterAttachmentDto
{
    /// <summary>附件类型：shell / coupon / item。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>道具/非遗 ID（type=item 时有值）。</summary>
    public string? ItemId { get; set; }

    /// <summary>数量。</summary>
    public int Count { get; set; }
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

    /// <summary>领取到的附件列表（多附件）。</summary>
    public List<LetterAttachmentDto> Attachments { get; set; } = new();
}
