namespace WildCourt.Data.Entities;

/// <summary>好友申请记录（A 申请 B，B 接受后写入 friends 表）。</summary>
public class FriendRequestEntity
{
    public long Id { get; set; }

    /// <summary>申请人用户 ID。</summary>
    public long FromUserId { get; set; }

    /// <summary>被申请人用户 ID。</summary>
    public long ToUserId { get; set; }

    /// <summary>申请状态：pending（待处理）/ accepted（已接受）/ rejected（已拒绝）。</summary>
    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
