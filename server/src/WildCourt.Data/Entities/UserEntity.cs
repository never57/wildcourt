namespace WildCourt.Data.Entities;

/// <summary>
/// 用户表（表名 users）。
/// 对应文档 15.3 数据库表结构：user_id, nickname, province_id, city_id, level, created_at, last_login；
/// 登录所需 username / password_hash（AuthService 约定：客户端 SHA256 一次传输，服务端 bcrypt 存储）。
/// </summary>
public class UserEntity
{
    public long UserId { get; set; }

    /// <summary>登录账号（唯一，4~20 位字母数字下划线）。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>密码哈希（客户端 SHA256 hex → 服务端 BCrypt）。</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    /// <summary>头像 URL（运营后台可修改；客户端上传后回填）。</summary>
    public string? AvatarUrl { get; set; }

    public int ProvinceId { get; set; }

    public int CityId { get; set; }

    public int Level { get; set; }

    /// <summary>庭贝（日常软货币，文档 09 §9.1）。</summary>
    public int Shell { get; set; } = 0;

    /// <summary>邮券（活动限定货币，仅活动产出，文档 09 §9.1.1）。</summary>
    public int Coupon { get; set; } = 0;

    /// <summary>玩家经验（等级由经验驱动，文档 03 §3.5；经验不设上限存储，等级封顶 4）。</summary>
    public int Exp { get; set; } = 0;

    /// <summary>是否被封禁（管理后台封禁后禁止登录，并踢出全部会话）。</summary>
    public bool IsBanned { get; set; } = false;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLogin { get; set; }
}
