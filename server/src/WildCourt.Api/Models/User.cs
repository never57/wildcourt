namespace WildCourt.Api.Models;

/// <summary>
/// 玩家信息模型（对应 users 表）。
/// </summary>
public class User
{
    public long UserId { get; set; }

    public string Nickname { get; set; } = string.Empty;

    /// <summary>头像 URL（客户端上传后回填；可为空）。</summary>
    public string? AvatarUrl { get; set; }

    public int ProvinceId { get; set; }

    public int CityId { get; set; }

    public int Level { get; set; }

    /// <summary>庭贝（日常软货币，文档 09 §9.1）。</summary>
    public int Shell { get; set; }

    /// <summary>邮券（活动限定货币，仅活动产出，文档 09 §9.1.1）。</summary>
    public int Coupon { get; set; }

    /// <summary>玩家经验（等级由经验驱动，文档 03 §3.5）。</summary>
    public int Exp { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLogin { get; set; }
}
