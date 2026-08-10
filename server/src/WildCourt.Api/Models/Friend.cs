namespace WildCourt.Api.Models;

/// <summary>
/// 好友关系模型（对应 friends 表）。
/// </summary>
public class Friend
{
    public long UserId { get; set; }

    public long FriendId { get; set; }

    public DateTime CreatedAt { get; set; }
}
