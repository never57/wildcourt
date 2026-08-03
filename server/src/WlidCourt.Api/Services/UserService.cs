using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WlidCourt.Api.Models;
using WlidCourt.Api.Utils;
using WlidCourt.Data.DbContext;
using WlidCourt.Data.Entities;

namespace WlidCourt.Api.Services;

/// <summary>
/// 用户业务：注册/登录/身份；会话签发（文档 02 §2.3）。
/// 密码约定（AuthService）：客户端 SHA256 一次哈希传输 → 服务端 BCrypt 加盐存储。
/// </summary>
public partial class UserService
{
    private readonly WlidCourtDbContext _db;
    private readonly SessionStore _sessions;

    public UserService(WlidCourtDbContext db, SessionStore sessions)
    {
        _db = db;
        _sessions = sessions;
    }

    /// <summary>
    /// 注册（选省/市）。账号已存在或格式非法抛 <see cref="BusinessException"/>。
    /// </summary>
    public async Task<User> RegisterAsync(string username, string passwordHash)
    {
        username = username.Trim();
        if (!UsernameRegex().IsMatch(username))
        {
            throw new BusinessException(ApiCodes.BadRequest, "账号须为 4~20 位字母、数字或下划线");
        }
        if (string.IsNullOrEmpty(passwordHash))
        {
            throw new BusinessException(ApiCodes.BadRequest, "密码不能为空");
        }

        if (await _db.Users.AnyAsync(u => u.Username == username))
        {
            throw new BusinessException(ApiCodes.Conflict, "账号已存在");
        }

        var entity = new UserEntity
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordHash),
            Nickname = username,
            Level = 1,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(entity);
        await _db.SaveChangesAsync();

        return ToModel(entity);
    }

    /// <summary>
    /// 登录：校验账号密码 → 签发会话 token（user_session:{token}）→ 更新 last_login。
    /// </summary>
    public async Task<LoginResponse> LoginAsync(string username, string passwordHash)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Username == username.Trim());
        if (entity == null || !BCrypt.Net.BCrypt.Verify(passwordHash, entity.PasswordHash))
        {
            throw new BusinessException(ApiCodes.Unauthorized, "账号或密码错误");
        }

        entity.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new LoginResponse
        {
            Token = _sessions.Create(entity.UserId),
            UserId = entity.UserId,
        };
    }

    /// <summary>玩家信息；不存在返回 null（由 Controller 转 404）。</summary>
    public async Task<User?> GetProfileAsync(long userId)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        return entity == null ? null : ToModel(entity);
    }

    private static User ToModel(UserEntity e) => new()
    {
        UserId = e.UserId,
        Nickname = e.Nickname,
        ProvinceId = e.ProvinceId,
        CityId = e.CityId,
        Level = e.Level,
        CreatedAt = e.CreatedAt,
        LastLogin = e.LastLogin,
    };

    [GeneratedRegex(@"^[a-zA-Z0-9_]{4,20}$")]
    private static partial Regex UsernameRegex();
}
