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

    /// <summary>庭贝（日常软货币，文档 09 §9.1）。</summary>
    public int Shell { get; set; }

    /// <summary>邮券（活动限定货币，仅活动产出，文档 09 §9.1.1）。</summary>
    public int Coupon { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLogin { get; set; }
}
