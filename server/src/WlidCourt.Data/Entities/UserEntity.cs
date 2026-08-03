namespace WlidCourt.Data.Entities;

/// <summary>
/// 用户表（表名 users）。
/// 对应文档 15.3 数据库表结构：user_id, nickname, province_id, city_id, level, created_at, last_login。
/// </summary>
public class UserEntity
{
    public long UserId { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public int ProvinceId { get; set; }

    public int CityId { get; set; }

    public int Level { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLogin { get; set; }
}
