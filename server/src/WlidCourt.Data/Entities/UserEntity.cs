namespace WlidCourt.Data.Entities;

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

    public int ProvinceId { get; set; }

    public int CityId { get; set; }

    public int Level { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLogin { get; set; }
}
