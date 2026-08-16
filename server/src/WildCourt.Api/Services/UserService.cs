using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Api.Utils;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 用户业务：注册/登录/身份；会话签发（文档 02 §2.3）。
/// 密码约定（AuthService）：客户端 SHA256 一次哈希传输 → 服务端 BCrypt 加盐存储。
/// </summary>
public partial class UserService
{
    /// <summary>新玩家初始庭贝（阶段 A 联调用；后续随数值配置表规范挪入 Config_*.json）。</summary>
    public const int InitialShell = 1000;

    private readonly WildCourtDbContext _db;
    private readonly ISessionStore _sessions;
    private readonly YardService _yard;

    public UserService(WildCourtDbContext db, ISessionStore sessions, YardService yard)
    {
        _db = db;
        _sessions = sessions;
        _yard = yard;
    }

    /// <summary>
    /// 注册（选省/市）。账号已存在或格式非法抛 <see cref="BusinessException"/>。
    /// 并发同名注册依赖 username 唯一索引兜底（DbUpdateException → 409）。
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
            Shell = InitialShell,
            Coupon = 0,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // 并发同名注册：唯一索引冲突（TOCTOU 兜底，避免 500）
            throw new BusinessException(ApiCodes.Conflict, "账号已存在");
        }

        // 注册即建默认庭院（文档 03：注册后可直接进庭院）
        await _yard.CreateDefaultAsync(entity.UserId);

        return ToModel(entity);
    }

    /// <summary>
    /// 登录：校验账号密码 → 签发会话 token（Redis user_session:{token}）→ 更新 last_login。
    /// </summary>
    public async Task<LoginResponse> LoginAsync(string username, string passwordHash)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Username == username.Trim());
        if (entity == null || !BCrypt.Net.BCrypt.Verify(passwordHash, entity.PasswordHash))
        {
            throw new BusinessException(ApiCodes.Unauthorized, "账号或密码错误");
        }
        if (entity.IsBanned)
        {
            throw new BusinessException(ApiCodes.Forbidden, "账号已被封禁，请联系客服");
        }

        entity.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new LoginResponse
        {
            Token = await _sessions.CreateAsync(entity.UserId),
            UserId = entity.UserId,
        };
    }

    /// <summary>玩家信息；不存在返回 null（由 Controller 转 404）。</summary>
    public async Task<User?> GetProfileAsync(long userId)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        return entity == null ? null : ToModel(entity);
    }

    /// <summary>
    /// 玩家自助修改昵称（非空、Trim、≤32 字符）。修改成功后返回最新玩家信息。
    /// </summary>
    public async Task<User?> UpdateNicknameAsync(long userId, string nickname)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "用户不存在");

        var trimmed = nickname?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new BusinessException(ApiCodes.BadRequest, "昵称不能为空");
        }
        if (trimmed.Length > 32)
        {
            throw new BusinessException(ApiCodes.BadRequest, "昵称最多 32 个字符");
        }

        entity.Nickname = trimmed;
        await _db.SaveChangesAsync();
        return ToModel(entity);
    }

    /// <summary>更新头像 URL（客户端上传图片后回填；≤512 字符）。返回最新玩家信息。</summary>
    public async Task<User?> UpdateAvatarAsync(long userId, string avatarUrl)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "用户不存在");

        var url = avatarUrl?.Trim();
        if (url != null && url.Length > 512)
        {
            throw new BusinessException(ApiCodes.BadRequest, "头像地址过长");
        }
        entity.AvatarUrl = string.IsNullOrEmpty(url) ? null : url;
        await _db.SaveChangesAsync();
        return ToModel(entity);
    }

    /// <summary>背包快照（阶段 C：编辑面板“已拥有”状态，数量 &gt; 0 才返回）。</summary>
    public async Task<InventoryResponse> GetInventoryAsync(long userId)
    {
        var items = await _db.Inventories
            .Where(i => i.UserId == userId && i.Count > 0)
            .OrderBy(i => i.ItemId)
            .Select(i => new InventoryItemDto { ItemId = i.ItemId, Count = i.Count })
            .ToListAsync();
        return new InventoryResponse { Items = items };
    }

    private static User ToModel(UserEntity e) => new()
    {
        UserId = e.UserId,
        Nickname = e.Nickname,
        AvatarUrl = e.AvatarUrl,
        ProvinceId = e.ProvinceId,
        CityId = e.CityId,
        Level = e.Level,
        Shell = e.Shell,
        Coupon = e.Coupon,
        Exp = e.Exp,
        CreatedAt = e.CreatedAt,
        LastLogin = e.LastLogin,
    };

    [GeneratedRegex(@"^[a-zA-Z0-9_]{4,20}$")]
    private static partial Regex UsernameRegex();
}
