namespace WildCourt.Data.Entities;

/// <summary>
/// 运营后台管理员账号（表名 admins）。
/// 密码约定与玩家一致：客户端 SHA256 一次传输 → 服务端 BCrypt 存储。
/// </summary>
public class AdminEntity
{
    public long AdminId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>是否启用；停用后令牌即刻失效（鉴权时校验）。</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
}
