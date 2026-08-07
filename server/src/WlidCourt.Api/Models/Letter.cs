namespace WlidCourt.Api.Models;

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

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
