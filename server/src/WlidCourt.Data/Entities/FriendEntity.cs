namespace WlidCourt.Data.Entities;

/// <summary>
/// 好友关系表（表名 friends）。
/// 对应文档 15.3：user_id, friend_id, created_at。
/// </summary>
public class FriendEntity
{
    public long UserId { get; set; }

    public long FriendId { get; set; }

    public DateTime CreatedAt { get; set; }
}
