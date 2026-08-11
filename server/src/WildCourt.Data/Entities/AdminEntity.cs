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

    /// <summary>RBAC 角色：superadmin / operator / support / readonly。</summary>
    public string Role { get; set; } = AdminRoleDefaults.DefaultRole;

    /// <summary>是否启用；停用后令牌即刻失效（鉴权时校验）。</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    // ============ 双因素认证 ============

    /// <summary>是否已启用 TOTP 双因素认证。</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>TOTP 密钥（Base32 编码），启用后不可明文读取。</summary>
    public string? TwoFactorSecret { get; set; }
}

public static class AdminRoleDefaults
{
    public const string DefaultRole = "operator";
}
