namespace WildCourt.Data.Entities;

/// <summary>好友私聊消息（入库，支持离线消息 + 历史记录）。</summary>
public class ChatMessageEntity
{
    public long Id { get; set; }

    /// <summary>发送者用户 ID。</summary>
    public long FromUserId { get; set; }

    /// <summary>接收者用户 ID。</summary>
    public long ToUserId { get; set; }

    /// <summary>消息内容（文本）。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>接收者是否已读。</summary>
    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
