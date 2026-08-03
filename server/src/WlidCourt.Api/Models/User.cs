namespace WlidCourt.Api.Models;

/// <summary>
/// 玩家信息模型（对应 users 表）。
/// </summary>
public class User
{
    public long UserId { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public int ProvinceId { get; set; }

    public int CityId { get; set; }

    public int Level { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLogin { get; set; }
}
