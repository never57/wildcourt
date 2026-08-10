using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Api.Utils;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 运营后台业务：管理员认证、玩家管理、仪表盘统计、公告、信件审核。
/// 密码约定与玩家一致：客户端 SHA256 一次哈希传输 → 服务端 BCrypt 存储。
/// </summary>
public class AdminService
{
    private readonly WildCourtDbContext _db;
    private readonly RedisUtil _redis;
    private readonly AdminTokenService _tokens;

    public AdminService(WildCourtDbContext db, RedisUtil redis, AdminTokenService tokens)
    {
        _db = db;
        _redis = redis;
        _tokens = tokens;
    }

    // ============ 认证 ============

    /// <summary>管理员登录：校验密码 → 签发运营令牌 → 记录登录时间。</summary>
    public async Task<AdminLoginResponse> LoginAsync(string username, string passwordSha256)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Username == username.Trim());
        if (admin == null || !BCrypt.Net.BCrypt.Verify(passwordSha256, admin.PasswordHash))
        {
            throw new BusinessException(ApiCodes.Unauthorized, "账号或密码错误");
        }
        if (!admin.IsActive)
        {
            throw new BusinessException(ApiCodes.Forbidden, "账号已被停用");
        }

        admin.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AdminLoginResponse
        {
            Token = _tokens.Issue(admin.AdminId),
            Username = admin.Username,
            DisplayName = admin.DisplayName,
            ExpireAt = DateTime.UtcNow.AddDays(1),
        };
    }

    public async Task<AdminProfileResponse> GetProfileAsync(long adminId)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId)
            ?? throw new BusinessException(ApiCodes.NotFound, "管理员不存在");
        return new AdminProfileResponse
        {
            AdminId = admin.AdminId,
            Username = admin.Username,
            DisplayName = admin.DisplayName,
            LastLoginAt = admin.LastLoginAt,
        };
    }

    /// <summary>修改密码：需验证旧密码（均为客户端 SHA256 hex）。</summary>
    public async Task ChangePasswordAsync(long adminId, string oldPasswordSha256, string newPasswordSha256)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId)
            ?? throw new BusinessException(ApiCodes.NotFound, "管理员不存在");
        if (!BCrypt.Net.BCrypt.Verify(oldPasswordSha256, admin.PasswordHash))
        {
            throw new BusinessException(ApiCodes.BadRequest, "旧密码错误");
        }
        if (string.IsNullOrEmpty(newPasswordSha256) || newPasswordSha256.Length < 6)
        {
            throw new BusinessException(ApiCodes.BadRequest, "新密码过短（SHA256 后至少 6 位）");
        }
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPasswordSha256);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AdminItemDto>> ListAdminsAsync()
    {
        return await _db.Admins
            .OrderBy(a => a.AdminId)
            .Select(a => new AdminItemDto
            {
                AdminId = a.AdminId,
                Username = a.Username,
                DisplayName = a.DisplayName,
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt,
                LastLoginAt = a.LastLoginAt,
            })
            .ToListAsync();
    }

    public async Task<long> CreateAdminAsync(string username, string passwordSha256, string displayName)
    {
        username = username.Trim();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(passwordSha256))
        {
            throw new BusinessException(ApiCodes.BadRequest, "账号与密码不能为空");
        }
        if (await _db.Admins.AnyAsync(a => a.Username == username))
        {
            throw new BusinessException(ApiCodes.Conflict, "管理员账号已存在");
        }
        var admin = new AdminEntity
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordSha256),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();
        return admin.AdminId;
    }

    public async Task SetAdminActiveAsync(long adminId, bool isActive)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId)
            ?? throw new BusinessException(ApiCodes.NotFound, "管理员不存在");
        admin.IsActive = isActive;
        await _db.SaveChangesAsync();
    }

    /// <summary>重置管理员密码（管理员操作，不需要旧密码）。</summary>
    public async Task ResetAdminPasswordAsync(long adminId, string newPasswordSha256)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId)
            ?? throw new BusinessException(ApiCodes.NotFound, "管理员不存在");
        if (string.IsNullOrEmpty(newPasswordSha256) || newPasswordSha256.Length < 6)
        {
            throw new BusinessException(ApiCodes.BadRequest, "新密码过短（SHA256 后至少 6 位）");
        }
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPasswordSha256);
        await _db.SaveChangesAsync();
    }

    /// <summary>删除管理员。不能删除当前登录账号，且至少保留一个启用的管理员。</summary>
    public async Task DeleteAdminAsync(long adminId, long operatorAdminId)
    {
        if (adminId == operatorAdminId)
        {
            throw new BusinessException(ApiCodes.BadRequest, "不能删除当前登录账号");
        }
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId)
            ?? throw new BusinessException(ApiCodes.NotFound, "管理员不存在");
        var activeCount = await _db.Admins.CountAsync(a => a.IsActive);
        if (activeCount <= 1)
        {
            throw new BusinessException(ApiCodes.BadRequest, "至少保留一个启用的管理员账号");
        }
        _db.Admins.Remove(admin);
        await _db.SaveChangesAsync();
    }

    // ============ 仪表盘统计 ============

    public async Task<DashboardSummaryResponse> GetDashboardSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = todayStart.AddDays(-6);

        var totalPlayers = await _db.Users.LongCountAsync();
        var todayNew = await _db.Users.LongCountAsync(u => u.CreatedAt >= todayStart);
        var todayActive = await _db.Users.LongCountAsync(u => u.LastLogin != null && u.LastLogin >= todayStart);
        var online = await _redis.CountKeysAsync(RedisUtil.OnlineKeyPrefix + "*");
        var totalShell = await _db.Users.SumAsync(u => (long)u.Shell);
        var totalCoupon = await _db.Users.SumAsync(u => (long)u.Coupon);

        var raw = await _db.Users
            .Where(u => u.CreatedAt >= weekStart)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new DailyCountDto { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.LongCount() })
            .ToListAsync();
        var trend = Enumerable.Range(0, 7)
            .Select(i => weekStart.AddDays(i).ToString("yyyy-MM-dd"))
            .Select(d => new DailyCountDto { Date = d, Count = raw.FirstOrDefault(r => r.Date == d)?.Count ?? 0 })
            .ToList();

        return new DashboardSummaryResponse
        {
            TotalPlayers = totalPlayers,
            TodayNewPlayers = todayNew,
            OnlinePlayers = online,
            TodayActivePlayers = todayActive,
            TotalShell = totalShell,
            TotalCoupon = totalCoupon,
            RegistrationTrend = trend,
        };
    }

    // ============ 玩家管理 ============

    public async Task<PagedResult<PlayerAdminDto>> SearchPlayersAsync(string? keyword, int page, int pageSize)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            var uid = long.TryParse(kw, out var parsedUid) ? parsedUid : (long?)null;
            query = query.Where(u =>
                u.Username.Contains(kw) ||
                u.Nickname.Contains(kw) ||
                (uid != null && u.UserId == uid));
        }

        var total = await query.LongCountAsync();
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new PlayerAdminDto
            {
                UserId = u.UserId,
                Username = u.Username,
                Nickname = u.Nickname,
                AvatarUrl = u.AvatarUrl,
                Level = u.Level,
                Shell = u.Shell,
                Coupon = u.Coupon,
                Exp = u.Exp,
                IsBanned = u.IsBanned,
                CreatedAt = u.CreatedAt,
                LastLogin = u.LastLogin,
            })
            .ToListAsync();

        return new PagedResult<PlayerAdminDto> { Items = items, Total = total };
    }

    public async Task<PlayerDetailDto> GetPlayerDetailAsync(long userId)
    {
        var player = await _db.Users
            .Where(u => u.UserId == userId)
            .Select(u => new PlayerAdminDto
            {
                UserId = u.UserId,
                Username = u.Username,
                Nickname = u.Nickname,
                AvatarUrl = u.AvatarUrl,
                Level = u.Level,
                Shell = u.Shell,
                Coupon = u.Coupon,
                Exp = u.Exp,
                IsBanned = u.IsBanned,
                CreatedAt = u.CreatedAt,
                LastLogin = u.LastLogin,
            })
            .FirstOrDefaultAsync()
            ?? throw new BusinessException(ApiCodes.NotFound, "玩家不存在");

        var pets = await _db.Pets
            .Where(p => p.UserId == userId)
            .Select(p => new PetAdminDto
            {
                PetId = p.PetId,
                Type = p.Type,
                Level = p.Level,
                Exp = p.Exp,
                Status = p.Status,
            })
            .ToListAsync();

        var inventory = await _db.Inventories
            .Where(i => i.UserId == userId && i.Count > 0)
            .OrderBy(i => i.ItemId)
            .Select(i => new InventoryItemDto { ItemId = i.ItemId, Count = i.Count })
            .ToListAsync();

        var friendCount = await _db.Friends.LongCountAsync(f => f.UserId == userId);

        return new PlayerDetailDto
        {
            Player = player,
            Pets = pets,
            Inventory = inventory,
            FriendCount = (int)friendCount,
        };
    }

    /// <summary>修改玩家货币/等级/经验（运营补发/回档用，值为覆盖语义）。</summary>
    public async Task UpdatePlayerCurrencyAsync(long userId, UpdatePlayerCurrencyRequest req)
    {
        var player = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "玩家不存在");

        if (req.Shell.HasValue) player.Shell = Math.Max(0, req.Shell.Value);
        if (req.Coupon.HasValue) player.Coupon = Math.Max(0, req.Coupon.Value);
        if (req.Level.HasValue) player.Level = Math.Max(1, req.Level.Value);
        if (req.Exp.HasValue) player.Exp = Math.Max(0, req.Exp.Value);

        await _db.SaveChangesAsync();
    }

    /// <summary>修改玩家昵称/头像等基础资料（字段为 null 表示不修改）。</summary>
    public async Task UpdatePlayerProfileAsync(long userId, UpdatePlayerProfileRequest req)
    {
        var player = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "玩家不存在");

        if (req.Nickname != null)
        {
            var nickname = req.Nickname.Trim();
            if (string.IsNullOrEmpty(nickname))
            {
                throw new BusinessException(ApiCodes.BadRequest, "昵称不能为空");
            }
            if (nickname.Length > 32)
            {
                throw new BusinessException(ApiCodes.BadRequest, "昵称最多 32 个字符");
            }
            player.Nickname = nickname;
        }

        if (req.AvatarUrl != null)
        {
            var avatar = req.AvatarUrl.Trim();
            if (avatar.Length > 512)
            {
                throw new BusinessException(ApiCodes.BadRequest, "头像地址过长");
            }
            player.AvatarUrl = string.IsNullOrEmpty(avatar) ? null : avatar;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>封禁：标记 is_banned + 踢出全部在线会话。已被封禁返回 true（幂等）。</summary>
    public async Task<bool> BanAsync(long userId)
    {
        var player = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "玩家不存在");
        if (player.IsBanned)
        {
            return false;
        }
        player.IsBanned = true;
        await _db.SaveChangesAsync();
        await _redis.DeleteSessionsOfUserAsync(userId);
        // 同时清掉实时在线标记，封禁后立即从"实时在线"移除
        await _redis.DeleteAsync(RedisUtil.OnlineKeyPrefix + userId);
        return true;
    }

    /// <summary>解封。未被封禁返回 false（幂等）。</summary>
    public async Task<bool> UnbanAsync(long userId)
    {
        var player = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "玩家不存在");
        if (!player.IsBanned)
        {
            return false;
        }
        player.IsBanned = false;
        await _db.SaveChangesAsync();
        return true;
    }

    // ============ 公告 ============

    public async Task<List<AnnouncementDto>> ListAnnouncementsAsync()
    {
        return await _db.Announcements
            .OrderByDescending(a => a.AnnouncementId)
            .Select(a => new AnnouncementDto
            {
                AnnouncementId = a.AnnouncementId,
                Title = a.Title,
                Content = a.Content,
                Status = a.Status,
                CreatorName = a.CreatedBy.ToString(),
                CreatedAt = a.CreatedAt,
                PublishedAt = a.PublishedAt,
            })
            .ToListAsync();
    }

    public async Task<long> CreateAnnouncementAsync(long adminId, AnnouncementRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Content))
        {
            throw new BusinessException(ApiCodes.BadRequest, "标题与内容不能为空");
        }
        var entity = new AnnouncementEntity
        {
            Title = req.Title.Trim(),
            Content = req.Content,
            Status = req.Status == "published" ? "published" : "draft",
            CreatedBy = adminId,
            CreatedAt = DateTime.UtcNow,
            PublishedAt = req.Status == "published" ? DateTime.UtcNow : null,
        };
        _db.Announcements.Add(entity);
        await _db.SaveChangesAsync();
        return entity.AnnouncementId;
    }

    public async Task UpdateAnnouncementAsync(long id, AnnouncementRequest req)
    {
        var entity = await _db.Announcements.FirstOrDefaultAsync(a => a.AnnouncementId == id)
            ?? throw new BusinessException(ApiCodes.NotFound, "公告不存在");
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Content))
        {
            throw new BusinessException(ApiCodes.BadRequest, "标题与内容不能为空");
        }
        entity.Title = req.Title.Trim();
        entity.Content = req.Content;
        if (req.Status == "published" && entity.Status != "published")
        {
            entity.PublishedAt = DateTime.UtcNow;
        }
        entity.Status = req.Status == "published" ? "published"
            : req.Status == "archived" ? "archived" : "draft";
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAnnouncementAsync(long id)
    {
        var entity = await _db.Announcements.FirstOrDefaultAsync(a => a.AnnouncementId == id)
            ?? throw new BusinessException(ApiCodes.NotFound, "公告不存在");
        _db.Announcements.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task PublishAnnouncementAsync(long id)
    {
        var entity = await _db.Announcements.FirstOrDefaultAsync(a => a.AnnouncementId == id)
            ?? throw new BusinessException(ApiCodes.NotFound, "公告不存在");
        entity.Status = "published";
        entity.PublishedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ============ 信件审核 ============

    /// <summary>审核队列：待审核信件 + 收发件人昵称。</summary>
    public async Task<List<ReviewQueueItemDto>> GetAuditQueueAsync()
    {
        var pending = await _db.Letters
            .Where(l => l.Status == "待审核")
            .OrderBy(l => l.CreatedAt)
            .Take(200)
            .ToListAsync();

        var ids = pending.Select(l => l.FromUser).Concat(pending.Select(l => l.ToUser)).Distinct().ToList();
        var nicknames = await _db.Users
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Nickname);

        return pending.Select(l => new ReviewQueueItemDto
        {
            LetterId = l.LetterId,
            FromUser = l.FromUser,
            FromNickname = nicknames.GetValueOrDefault(l.FromUser, "未知"),
            ToUser = l.ToUser,
            ToNickname = nicknames.GetValueOrDefault(l.ToUser, "未知"),
            Content = l.Content,
            ItemId = l.ItemId,
            Status = l.Status,
            CreatedAt = l.CreatedAt,
        }).ToList();
    }

    /// <summary>审核信件：通过 → 状态“通过”（并触发送达投递占位）；拒绝 → 状态“退回”。</summary>
    public async Task ReviewLetterAsync(long letterId, bool approve)
    {
        var letter = await _db.Letters.FirstOrDefaultAsync(l => l.LetterId == letterId)
            ?? throw new BusinessException(ApiCodes.NotFound, "信件不存在");
        if (letter.Status != "待审核")
        {
            throw new BusinessException(ApiCodes.Conflict, "该信件已审核");
        }
        letter.Status = approve ? "通过" : "退回";
        // TODO: 通过后进入投递流程（LetterService.DeliverAsync 占位，骨架阶段直接改状态）。
        await _db.SaveChangesAsync();
    }
}
