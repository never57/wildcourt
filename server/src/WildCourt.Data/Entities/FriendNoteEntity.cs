namespace WildCourt.Data.Entities;

/// <summary>
/// 好友备注表（表名 friend_notes）。
/// 备注是单向的：userId 给 friendId 设置的备注，仅影响 userId 视角的显示，对方不可见。
/// 主键 (UserId, FriendId)。
/// </summary>
public class FriendNoteEntity
{
    public long UserId { get; set; }

    public long FriendId { get; set; }

    /// <summary>备注文本（空字符串表示清除备注）。</summary>
    public string Note { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
