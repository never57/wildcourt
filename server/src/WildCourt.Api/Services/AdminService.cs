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
    private readonly CaptchaService _captcha;
    private readonly TotpService _totp;

    // 登录失败锁定配置：5 次失败锁定 15 分钟
    private const int MaxLoginFails = 5;
    private static readonly TimeSpan LoginLockDuration = TimeSpan.FromMinutes(15);
    private const string LoginFailKeyPrefix = "admin_login_fail:";
    private const string LoginLockKeyPrefix = "admin_login_lock:";
    private const string CaptchaKeyPrefix = "admin_captcha:";

    public AdminService(WildCourtDbContext db, RedisUtil redis, AdminTokenService tokens,
        CaptchaService captcha, TotpService totp)
    {
        _db = db;
        _redis = redis;
        _tokens = tokens;
        _captcha = captcha;
        _totp = totp;
    }

    // ============ 验证码 ============

    /// <summary>生成图形验证码：Redis 存 code（5 分钟），返回 id + SVG base64。</summary>
    public async Task<CaptchaResponse> GetCaptchaAsync()
    {
        var (code, image) = _captcha.Generate();
        var captchaId = Guid.NewGuid().ToString("N");
        await _redis.SetStringAsync(CaptchaKeyPrefix + captchaId, code, TimeSpan.FromMinutes(5));
        return new CaptchaResponse { CaptchaId = captchaId, Image = image };
    }

    private async Task<bool> ValidateCaptchaAsync(string? captchaId, string? captchaCode)
    {
        if (string.IsNullOrWhiteSpace(captchaId) || string.IsNullOrWhiteSpace(captchaCode))
        {
            return false;
        }
        var key = CaptchaKeyPrefix + captchaId;
        var stored = await _redis.GetStringAsync(key);
        if (stored is null) return false;
        // 一次性使用，无论对错都删除
        await _redis.DeleteAsync(key);
        return string.Equals(stored, captchaCode.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    // ============ 认证 ============

    /// <summary>
    /// 管理员登录：
    /// 1. 校验验证码
    /// 2. 失败锁定检查（5 次失败锁 15 分钟）
    /// 3. 校验密码 → 未启用 2FA 直接签发令牌；已启用返回 NeedTotp + 临时票据
    /// </summary>
    public async Task<AdminLoginResponse> LoginAsync(string username, string passwordSha256,
        string? captchaId, string? captchaCode, string? totpCode, string? totpTicket)
    {
        username = username.Trim();

        // ---- 双因素第二步：直接用票据 + TOTP 完成登录 ----
        if (!string.IsNullOrEmpty(totpTicket))
        {
            return await CompleteTotpLoginAsync(username, totpTicket, totpCode);
        }

        // ---- 验证码校验 ----
        if (!await ValidateCaptchaAsync(captchaId, captchaCode))
        {
            throw new BusinessException(ApiCodes.BadRequest, "验证码错误或已过期");
        }

        // ---- 失败锁定检查 ----
        var lockKey = LoginLockKeyPrefix + username;
        var lockUntil = await _redis.GetStringAsync(lockKey);
        if (lockUntil is not null && DateTime.TryParse(lockUntil, out var until) && until > DateTime.UtcNow)
        {
            var remain = (int)(until - DateTime.UtcNow).TotalMinutes + 1;
            throw new BusinessException(ApiCodes.Forbidden, $"登录失败次数过多，请 {remain} 分钟后再试");
        }

        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Username == username);
        if (admin == null || !BCrypt.Net.BCrypt.Verify(passwordSha256, admin.PasswordHash))
        {
            await RecordLoginFailAsync(username);
            throw new BusinessException(ApiCodes.Unauthorized, "账号或密码错误");
        }
        if (!admin.IsActive)
        {
            throw new BusinessException(ApiCodes.Forbidden, "账号已被停用");
        }

        // 登录成功，清除失败计数
        await _redis.DeleteAsync(LoginFailKeyPrefix + username);
        await _redis.DeleteAsync(lockKey);

        // 已启用双因素：签发 5 分钟临时票据，进入第二步
        if (admin.TwoFactorEnabled)
        {
            return new AdminLoginResponse
            {
                Username = admin.Username,
                DisplayName = admin.DisplayName,
                Role = admin.Role,
                NeedTotp = true,
                TotpTicket = _tokens.Issue(admin.AdminId, TimeSpan.FromMinutes(5)),
                ExpireAt = DateTime.UtcNow.AddMinutes(5),
            };
        }

        return await IssueLoginAsync(admin);
    }

    private async Task<AdminLoginResponse> IssueLoginAsync(AdminEntity admin)
    {
        admin.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AdminLoginResponse
        {
            Token = _tokens.Issue(admin.AdminId),
            Username = admin.Username,
            DisplayName = admin.DisplayName,
            Role = admin.Role,
            ExpireAt = DateTime.UtcNow.AddDays(1),
        };
    }

    /// <summary>双因素第二步：校验 TOTP 后签发正式令牌。</summary>
    private async Task<AdminLoginResponse> CompleteTotpLoginAsync(string username, string totpTicket, string? totpCode)
    {
        var adminId = _tokens.TryValidate(totpTicket);
        if (adminId is null)
        {
            throw new BusinessException(ApiCodes.Unauthorized, "双因素会话已过期，请重新登录");
        }
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId.Value)
            ?? throw new BusinessException(ApiCodes.Unauthorized, "账号不存在");
        if (!admin.IsActive)
        {
            throw new BusinessException(ApiCodes.Forbidden, "账号已被停用");
        }
        if (!admin.TwoFactorEnabled || string.IsNullOrEmpty(admin.TwoFactorSecret))
        {
            throw new BusinessException(ApiCodes.BadRequest, "该账号未启用双因素认证");
        }
        if (!_totp.Validate(admin.TwoFactorSecret, totpCode))
        {
            throw new BusinessException(ApiCodes.Unauthorized, "动态验证码错误");
        }
        return await IssueLoginAsync(admin);
    }

    private async Task RecordLoginFailAsync(string username)
    {
        var failKey = LoginFailKeyPrefix + username;
        var countStr = await _redis.GetStringAsync(failKey);
        var count = int.TryParse(countStr, out var c) ? c + 1 : 1;
        if (count >= MaxLoginFails)
        {
            // 达到阈值：锁定并清除计数
            await _redis.SetStringAsync(LoginLockKeyPrefix + username,
                DateTime.UtcNow.Add(LoginLockDuration).ToString("O"), LoginLockDuration);
            await _redis.DeleteAsync(failKey);
        }
        else
        {
            await _redis.SetStringAsync(failKey, count.ToString(), LoginLockDuration);
        }
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
            Role = admin.Role,
            RoleName = AdminPermissions.GetRoleName(admin.Role),
            Permissions = AdminPermissions.GetPermissions(admin.Role).OrderBy(p => p).ToList(),
            TwoFactorEnabled = admin.TwoFactorEnabled,
            LastLoginAt = admin.LastLoginAt,
        };
    }

    // ============ 双因素（TOTP） ============

    /// <summary>生成双因素绑定密钥（未启用时才允许；已启用则报错）。</summary>
    public async Task<TwoFactorSetupResponse> SetupTwoFactorAsync(long adminId)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId)
            ?? throw new BusinessException(ApiCodes.NotFound, "管理员不存在");
        if (admin.TwoFactorEnabled)
        {
            throw new BusinessException(ApiCodes.BadRequest, "双因素认证已启用，请先关闭再重新绑定");
        }
        var secret = _totp.GenerateSecret();
        var uri = _totp.BuildOtpUri(secret, admin.Username);
        return new TwoFactorSetupResponse { Secret = secret, OtpUri = uri };
    }

    /// <summary>确认并启用双因素：校验一次性码后保存密钥。</summary>
    public async Task EnableTwoFactorAsync(long adminId, string secret, string code)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId)
            ?? throw new BusinessException(ApiCodes.NotFound, "管理员不存在");
        if (!_totp.Validate(secret, code))
        {
            throw new BusinessException(ApiCodes.BadRequest, "动态验证码错误");
        }
        admin.TwoFactorSecret = secret;
        admin.TwoFactorEnabled = true;
        await _db.SaveChangesAsync();
    }

    /// <summary>关闭双因素：校验当前动态码（或回退口令不存在）。</summary>
    public async Task DisableTwoFactorAsync(long adminId, string code)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId)
            ?? throw new BusinessException(ApiCodes.NotFound, "管理员不存在");
        if (!admin.TwoFactorEnabled || string.IsNullOrEmpty(admin.TwoFactorSecret))
        {
            throw new BusinessException(ApiCodes.BadRequest, "双因素认证未启用");
        }
        if (!_totp.Validate(admin.TwoFactorSecret, code))
        {
            throw new BusinessException(ApiCodes.BadRequest, "动态验证码错误");
        }
        admin.TwoFactorEnabled = false;
        admin.TwoFactorSecret = null;
        await _db.SaveChangesAsync();
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
                Role = a.Role,
                RoleName = a.Role,
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt,
                LastLoginAt = a.LastLoginAt,
            })
            .ToListAsync()
            .ContinueWith(t =>
            {
                foreach (var item in t.Result)
                {
                    item.RoleName = AdminPermissions.GetRoleName(item.Role);
                }
                return t.Result;
            });
    }

    public async Task<long> CreateAdminAsync(string username, string passwordSha256, string displayName, string? role = null)
    {
        username = username.Trim();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(passwordSha256))
        {
            throw new BusinessException(ApiCodes.BadRequest, "账号与密码不能为空");
        }
        AdminPermissions.EnsureValidRole(role);
        if (await _db.Admins.AnyAsync(a => a.Username == username))
        {
            throw new BusinessException(ApiCodes.Conflict, "管理员账号已存在");
        }
        var admin = new AdminEntity
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordSha256),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName,
            Role = string.IsNullOrWhiteSpace(role) ? AdminRoleDefaults.DefaultRole : role,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();
        return admin.AdminId;
    }

    /// <summary>修改管理员角色。不能修改自己的角色（防止降权锁死），且超管数量不能降为零。</summary>
    public async Task UpdateAdminRoleAsync(long adminId, string newRole, long operatorAdminId)
    {
        AdminPermissions.EnsureValidRole(newRole);
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId)
            ?? throw new BusinessException(ApiCodes.NotFound, "管理员不存在");
        if (adminId == operatorAdminId && admin.Role != newRole)
        {
            throw new BusinessException(ApiCodes.BadRequest, "不能修改自己的角色，请联系其他超管操作");
        }
        // 若把某超管降级，需保证仍至少有一个超管
        if (admin.Role == AdminPermissions.RoleSuperAdmin && newRole != AdminPermissions.RoleSuperAdmin)
        {
            var superCount = await _db.Admins.CountAsync(a => a.Role == AdminPermissions.RoleSuperAdmin && a.IsActive);
            if (superCount <= 1)
            {
                throw new BusinessException(ApiCodes.BadRequest, "系统至少保留一个超管账号");
            }
        }
        admin.Role = newRole;
        await _db.SaveChangesAsync();
    }

    /// <summary>角色定义列表（前端角色管理用）。</summary>
    public List<AdminRoleInfoDto> ListRoles() =>
        AdminPermissions.AllRoles
            .Select(r => new AdminRoleInfoDto
            {
                Role = r,
                Name = AdminPermissions.GetRoleName(r),
                Permissions = AdminPermissions.GetPermissions(r).OrderBy(p => p).ToList(),
            })
            .ToList();

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
        // 数据库时间均为 UTC 字面值；业务"今日/近 7 日"按北京时间（UTC+8）自然日计算
        var cnTodayStartUtc = DateTime.UtcNow.AddHours(8).Date.AddHours(-8); // 北京今日 00:00 → UTC 字面值
        var weekStart = cnTodayStartUtc.AddDays(-6);

        var totalPlayers = await _db.Users.LongCountAsync();
        var todayNew = await _db.Users.LongCountAsync(u => u.CreatedAt >= cnTodayStartUtc);
        var todayActive = await _db.Users.LongCountAsync(u => u.LastLogin != null && u.LastLogin >= cnTodayStartUtc);
        var online = await _redis.CountKeysAsync(RedisUtil.OnlineKeyPrefix + "*");
        var totalShell = await _db.Users.SumAsync(u => (long)u.Shell);
        var totalCoupon = await _db.Users.SumAsync(u => (long)u.Coupon);

        var raw = await _db.Users
            .Where(u => u.CreatedAt >= weekStart)
            .GroupBy(u => u.CreatedAt.AddHours(8).Date) // 转为北京日期再按天分组
            .Select(g => new DailyCountDto { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.LongCount() })
            .ToListAsync();
        var trend = Enumerable.Range(0, 7)
            .Select(i => weekStart.AddHours(8).AddDays(i).ToString("yyyy-MM-dd"))
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

    // ============ 官方信件 ============

    /// <summary>
    /// 官方发信：向指定玩家直接送达一封官方信件（from_user=0 标记系统），
    /// 附件按 attachments 携带多附件（shell/coupon/item，类型不可重复）。状态直接为“未读”，不经审核。
    /// </summary>
    public async Task<long> SendOfficialLetterAsync(OfficialLetterRequest req)
    {
        var attachments = ValidateLetterAttachments(req.Content, req.Attachments);

        var exists = await _db.Users.AnyAsync(u => u.UserId == req.ToUserId);
        if (!exists)
        {
            throw new BusinessException(ApiCodes.NotFound, "收件玩家不存在");
        }

        var entity = BuildLetter(req.ToUserId, req.Title, req.Content.Trim(), attachments);
        _db.Letters.Add(entity);
        await _db.SaveChangesAsync();
        return entity.LetterId;
    }

    /// <summary>批量发信：向多个玩家发送同一封官方信件，逐个失败不影响整体。</summary>
    public async Task<BatchOperationResult> SendOfficialLetterBatchAsync(OfficialLetterBatchRequest req)
    {
        if (req.ToUserIds == null || req.ToUserIds.Count == 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "请至少选择一个收件玩家");
        }
        if (req.ToUserIds.Count > 500)
        {
            throw new BusinessException(ApiCodes.BadRequest, "单次最多批量发送 500 个玩家");
        }

        var attachments = ValidateLetterAttachments(req.Content, req.Attachments);
        var ids = req.ToUserIds.Distinct().ToList();
        var existing = await _db.Users
            .Where(u => ids.Contains(u.UserId))
            .Select(u => u.UserId)
            .ToListAsync();
        var existingSet = existing.ToHashSet();

        var result = new BatchOperationResult();
        foreach (var id in ids)
        {
            if (!existingSet.Contains(id))
            {
                result.Failed++;
                result.Failures.Add(new BatchFailure { UserId = id, Reason = "玩家不存在" });
                continue;
            }
            _db.Letters.Add(BuildLetter(id, req.Title, req.Content.Trim(), attachments));
            result.Succeeded++;
        }

        await _db.SaveChangesAsync();
        return result;
    }

    /// <summary>按条件群发：按筛选条件匹配玩家后群发官方信件。</summary>
    public async Task<BatchOperationResult> BroadcastOfficialLetterAsync(OfficialLetterBroadcastRequest req)
    {
        var attachments = ValidateLetterAttachments(req.Content, req.Attachments);

        var query = _db.Users.AsQueryable();
        var filter = req.Filter;
        if (filter != null)
        {
            if (filter.MinLevel.HasValue) query = query.Where(u => u.Level >= filter.MinLevel.Value);
            if (filter.MaxLevel.HasValue) query = query.Where(u => u.Level <= filter.MaxLevel.Value);
            if (filter.IsBanned.HasValue) query = query.Where(u => u.IsBanned == filter.IsBanned.Value);
            if (filter.RegisteredAfter.HasValue) query = query.Where(u => u.CreatedAt >= filter.RegisteredAfter.Value);
        }

        var ids = await query.Select(u => u.UserId).ToListAsync();
        if (ids.Count == 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "没有匹配到符合条件的玩家");
        }
        if (ids.Count > 10000)
        {
            throw new BusinessException(ApiCodes.BadRequest, $"匹配到 {ids.Count} 个玩家，超过单次群发上限 10000，请缩小筛选范围");
        }

        var now = DateTime.UtcNow;
        foreach (var id in ids)
        {
            _db.Letters.Add(BuildLetter(id, req.Title, req.Content.Trim(), attachments, now));
        }

        await _db.SaveChangesAsync();
        return new BatchOperationResult { Succeeded = ids.Count, Failed = 0 };
    }

    // ============ 玩家批量操作 ============

    /// <summary>批量封禁：逐个执行，不存在的玩家计为失败但不中断。</summary>
    public async Task<BatchOperationResult> BanBatchAsync(List<long> userIds)
    {
        ValidateBatchIds(userIds);
        var ids = userIds.Distinct().ToList();
        var players = await _db.Users.Where(u => ids.Contains(u.UserId)).ToListAsync();
        var result = new BatchOperationResult();

        foreach (var id in ids)
        {
            var player = players.FirstOrDefault(p => p.UserId == id);
            if (player == null)
            {
                result.Failed++;
                result.Failures.Add(new BatchFailure { UserId = id, Reason = "玩家不存在" });
                continue;
            }
            if (player.IsBanned)
            {
                result.Failed++;
                result.Failures.Add(new BatchFailure { UserId = id, Reason = "已封禁" });
                continue;
            }
            player.IsBanned = true;
            result.Succeeded++;
        }

        await _db.SaveChangesAsync();

        // 批量踢下线 + 清在线标记（已封禁玩家）
        foreach (var player in players.Where(p => p.IsBanned && ids.Contains(p.UserId)))
        {
            await _redis.DeleteSessionsOfUserAsync(player.UserId);
            await _redis.DeleteAsync(RedisUtil.OnlineKeyPrefix + player.UserId);
        }

        return result;
    }

    /// <summary>批量解封。</summary>
    public async Task<BatchOperationResult> UnbanBatchAsync(List<long> userIds)
    {
        ValidateBatchIds(userIds);
        var ids = userIds.Distinct().ToList();
        var players = await _db.Users.Where(u => ids.Contains(u.UserId)).ToListAsync();
        var result = new BatchOperationResult();

        foreach (var id in ids)
        {
            var player = players.FirstOrDefault(p => p.UserId == id);
            if (player == null)
            {
                result.Failed++;
                result.Failures.Add(new BatchFailure { UserId = id, Reason = "玩家不存在" });
                continue;
            }
            if (!player.IsBanned)
            {
                result.Failed++;
                result.Failures.Add(new BatchFailure { UserId = id, Reason = "未被封禁" });
                continue;
            }
            player.IsBanned = false;
            result.Succeeded++;
        }

        await _db.SaveChangesAsync();
        return result;
    }

    /// <summary>批量修改货币/等级/经验（覆盖语义，字段为 null 表示不修改）。</summary>
    public async Task<BatchOperationResult> UpdateCurrencyBatchAsync(BatchUpdateCurrencyRequest req)
    {
        ValidateBatchIds(req.UserIds);
        var ids = req.UserIds.Distinct().ToList();
        var players = await _db.Users.Where(u => ids.Contains(u.UserId)).ToListAsync();
        var result = new BatchOperationResult();

        foreach (var id in ids)
        {
            var player = players.FirstOrDefault(p => p.UserId == id);
            if (player == null)
            {
                result.Failed++;
                result.Failures.Add(new BatchFailure { UserId = id, Reason = "玩家不存在" });
                continue;
            }
            if (req.Shell.HasValue) player.Shell = Math.Max(0, req.Shell.Value);
            if (req.Coupon.HasValue) player.Coupon = Math.Max(0, req.Coupon.Value);
            if (req.Level.HasValue) player.Level = Math.Max(1, req.Level.Value);
            if (req.Exp.HasValue) player.Exp = Math.Max(0, req.Exp.Value);
            result.Succeeded++;
        }

        await _db.SaveChangesAsync();
        return result;
    }

    // ============ 批量操作辅助 ============

    private void ValidateBatchIds(List<long> userIds)
    {
        if (userIds == null || userIds.Count == 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "请至少选择一个玩家");
        }
        if (userIds.Count > 500)
        {
            throw new BusinessException(ApiCodes.BadRequest, "单次最多操作 500 个玩家");
        }
    }

    /// <summary>校验信件正文/多附件参数，返回规范化后的附件实体列表（空=无附件）。</summary>
    private static List<LetterAttachment> ValidateLetterAttachments(string? content, List<LetterAttachmentDto>? attachments)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new BusinessException(ApiCodes.BadRequest, "信件内容不能为空");
        }

        if (attachments == null || attachments.Count == 0)
        {
            return new List<LetterAttachment>();
        }

        var allowedTypes = new[] { "shell", "coupon", "item" };
        var result = new List<LetterAttachment>();
        var seenTypes = new HashSet<string>();

        foreach (var a in attachments)
        {
            var type = (a.Type ?? string.Empty).Trim();
            if (!allowedTypes.Contains(type))
            {
                throw new BusinessException(ApiCodes.BadRequest, "附件类型不合法（可选：shell/coupon/item）");
            }
            if (a.Count <= 0)
            {
                throw new BusinessException(ApiCodes.BadRequest, "附件数量必须大于 0");
            }
            if (type == "item" && string.IsNullOrWhiteSpace(a.ItemId))
            {
                throw new BusinessException(ApiCodes.BadRequest, "道具附件需指定 ItemId");
            }

            // 类型不可重复：shell/coupon 全局唯一；item 按 ItemId 唯一
            var dupKey = type == "item" ? $"item:{a.ItemId}" : type;
            if (!seenTypes.Add(dupKey))
            {
                throw new BusinessException(ApiCodes.BadRequest, $"附件类型重复：{type}");
            }

            result.Add(new LetterAttachment
            {
                Type = type,
                ItemId = type == "item" ? a.ItemId?.Trim() : null,
                Count = a.Count,
            });
        }

        return result;
    }

    /// <summary>构造信件实体（from_user=0 表示官方，状态未读）。</summary>
    private static LetterEntity BuildLetter(long toUserId, string? title, string content,
        List<LetterAttachment> attachments, DateTime? createdAt = null)
    {
        return new LetterEntity
        {
            FromUser = 0, // 0 表示系统/官方
            ToUser = toUserId,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Content = content,
            Attachments = attachments,
            Status = "未读",
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
    }
}
