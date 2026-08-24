using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 好友业务：偷菜（文档 08 §8.3：30% 产量向下取整、每好友每作物每天 1 次、偷者每天 20 次、
/// 主人收获扣除已偷数量）。申请/接受、喂食仍为占位。
/// </summary>
public class FriendService
{
    private const string StealMechanicsFile = "Config_StealMechanics.json";
    private const string CropsFile = "Config_Crops.json";
    private const double DefaultStealRatio = 0.3;
    private const int DefaultDailyLimitPerCrop = 1;
    private const int DefaultDailyLimitTotal = 20;

    private readonly WildCourtDbContext _db;
    private readonly ConfigService _config;

    public FriendService(WildCourtDbContext db, ConfigService config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// 偷菜：校验好友关系/成熟/余量/每日限额 → 偷者入包、记录被偷数量、写流水（同事务原子提交）。
    /// </summary>
    public async Task<StealResultDto> StealAsync(long userId, long targetUserId, int cellId)
    {
        if (userId == targetUserId)
        {
            throw new BusinessException(ApiCodes.BadRequest, "不能偷自己的作物");
        }

        var isFriend = await _db.Friends.AnyAsync(f =>
            (f.UserId == userId && f.FriendId == targetUserId) ||
            (f.UserId == targetUserId && f.FriendId == userId));
        if (!isFriend)
        {
            throw new BusinessException(ApiCodes.BadRequest, "还不是好友，无法偷菜");
        }

        var plot = await _db.FarmPlots.FindAsync(targetUserId, cellId)
            ?? throw new BusinessException(ApiCodes.BadRequest, "该农田格没有作物");

        var stealConfig = _config.GetRows<StealMechanicsConfigRow>(StealMechanicsFile).FirstOrDefault();
        double stealRatio = stealConfig is { StealRatio: > 0 } ? stealConfig.StealRatio : DefaultStealRatio;
        int dailyLimitPerCrop = stealConfig is { DailyLimitPerCrop: > 0 } ? stealConfig.DailyLimitPerCrop : DefaultDailyLimitPerCrop;
        int dailyLimitTotal = stealConfig is { DailyLimitTotal: > 0 } ? stealConfig.DailyLimitTotal : DefaultDailyLimitTotal;

        if (stealConfig is not { MatureOnly: false } && plot.ReadyAt > DateTime.UtcNow)
        {
            throw new BusinessException(ApiCodes.BadRequest, "作物尚未成熟");
        }

        var crop = _config.GetRows<CropConfigRow>(CropsFile)
            .FirstOrDefault(c => string.Equals(c.Id, plot.CropId, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessException(ApiCodes.BadRequest, $"作物配置缺失: {plot.CropId}");

        int remaining = crop.Yield - plot.StolenCount;
        if (remaining <= 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "该作物已被偷光");
        }
        int stolen = Math.Min((int)Math.Floor(crop.Yield * stealRatio), remaining);
        if (stolen <= 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "无可偷产量");
        }

        // 每日限额（偷者维度，UTC+8 自然日）
        var today8 = DateTime.UtcNow.AddHours(8).Date;
        var myRecords = await _db.StealRecords
            .Where(r => r.StealerUserId == userId)
            .ToListAsync();
        int todayTotal = myRecords.Count(r => r.StolenAt.AddHours(8).Date == today8);
        int todayPerCrop = myRecords.Count(r =>
            r.StolenAt.AddHours(8).Date == today8 &&
            r.TargetUserId == targetUserId &&
            string.Equals(r.CropId, crop.Id, StringComparison.OrdinalIgnoreCase));
        if (todayPerCrop >= dailyLimitPerCrop)
        {
            throw new BusinessException(ApiCodes.BadRequest, "今天已偷过该好友的这个作物");
        }
        if (todayTotal >= dailyLimitTotal)
        {
            throw new BusinessException(ApiCodes.BadRequest, "今日偷菜次数已达上限");
        }

        // 偷者入包 + 记录被偷数量 + 流水
        var inventory = await _db.Inventories.FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == crop.Id);
        if (inventory == null)
        {
            _db.Inventories.Add(new InventoryEntity { UserId = userId, ItemId = crop.Id, Count = stolen });
        }
        else
        {
            inventory.Count += stolen;
        }

        plot.StolenCount += stolen;
        _db.StealRecords.Add(new StealRecordEntity
        {
            StealerUserId = userId,
            TargetUserId = targetUserId,
            CropId = crop.Id,
            CellId = cellId,
            Count = stolen,
            StolenAt = DateTime.UtcNow,
        });
        _db.Transactions.Add(new TransactionEntity
        {
            UserId = userId,
            Type = "steal",
            ItemId = crop.Id,
            Count = stolen,
            Price = 0,
        });

        await _db.SaveChangesAsync();

        return new StealResultDto { TargetUserId = targetUserId, CropId = crop.Id, Count = stolen };
    }

    /// <summary>好友申请：A 申请 B（待 B 接受后写入 friends 表）。幂等：已有 pending 申请则不重复建。</summary>
    public async Task ApplyAsync(long fromUserId, long toUserId)
    {
        if (fromUserId == toUserId)
        {
            throw new BusinessException(ApiCodes.BadRequest, "不能添加自己为好友");
        }
        var targetExists = await _db.Users.AnyAsync(u => u.UserId == toUserId);
        if (!targetExists)
        {
            throw new BusinessException(ApiCodes.NotFound, "目标玩家不存在");
        }
        var isFriend = await _db.Friends.AnyAsync(f =>
            (f.UserId == fromUserId && f.FriendId == toUserId) ||
            (f.UserId == toUserId && f.FriendId == fromUserId));
        if (isFriend)
        {
            throw new BusinessException(ApiCodes.Conflict, "你们已经是好友了");
        }

        var pending = await _db.FriendRequests.FirstOrDefaultAsync(r =>
            r.FromUserId == fromUserId && r.ToUserId == toUserId && r.Status == "pending");
        if (pending != null)
        {
            return; // 已有待处理申请，幂等
        }

        _db.FriendRequests.Add(new FriendRequestEntity
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>接受好友申请：把 pending 申请置为 accepted，并双向写入 friends 表。</summary>
    public async Task AcceptAsync(long userId, long fromUserId)
    {
        var request = await _db.FriendRequests.FirstOrDefaultAsync(r =>
            r.FromUserId == fromUserId && r.ToUserId == userId && r.Status == "pending")
            ?? throw new BusinessException(ApiCodes.NotFound, "好友申请不存在或已处理");

        var isFriend = await _db.Friends.AnyAsync(f =>
            (f.UserId == userId && f.FriendId == fromUserId) ||
            (f.UserId == fromUserId && f.FriendId == userId));
        if (!isFriend)
        {
            _db.Friends.Add(new FriendEntity { UserId = userId, FriendId = fromUserId, CreatedAt = DateTime.UtcNow });
        }

        request.Status = "accepted";
        await _db.SaveChangesAsync();
    }

    /// <summary>拒绝好友申请。</summary>
    public async Task RejectAsync(long userId, long fromUserId)
    {
        var request = await _db.FriendRequests.FirstOrDefaultAsync(r =>
            r.FromUserId == fromUserId && r.ToUserId == userId && r.Status == "pending")
            ?? throw new BusinessException(ApiCodes.NotFound, "好友申请不存在或已处理");

        request.Status = "rejected";
        await _db.SaveChangesAsync();
    }

    /// <summary>好友列表（含昵称/头像/等级）。</summary>
    public async Task<FriendListResponse> ListAsync(long userId)
    {
        var friendIds = await _db.Friends
            .Where(f => f.UserId == userId || f.FriendId == userId)
            .Select(f => f.UserId == userId ? f.FriendId : f.UserId)
            .Distinct()
            .ToListAsync();

        var users = await _db.Users
            .Where(u => friendIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u);

        // 统计每个好友发给我的未读私聊消息数（IsRead=false 且 ToUserId 为我），用于聊天未读角标
        var validIds = friendIds.Where(users.ContainsKey).ToList();
        var unreadCounts = await _db.ChatMessages
            .Where(m => m.ToUserId == userId && !m.IsRead && validIds.Contains(m.FromUserId))
            .GroupBy(m => m.FromUserId)
            .Select(g => new { FriendId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.FriendId, g => g.Count);

        // 查询我给每个好友设置的备注（单向，仅我视角可见）
        var notes = await _db.FriendNotes
            .Where(n => n.UserId == userId && validIds.Contains(n.FriendId))
            .ToDictionaryAsync(n => n.FriendId, n => n.Note);

        var list = validIds
            .Select(id =>
            {
                var u = users[id];
                unreadCounts.TryGetValue(id, out var unread);
                notes.TryGetValue(id, out var note);
                return new FriendDto
                {
                    UserId = u.UserId,
                    Nickname = u.Nickname,
                    AvatarUrl = u.AvatarUrl ?? string.Empty,
                    Level = u.Level,
                    UnreadCount = unread,
                    Note = note ?? string.Empty,
                };
            })
            .ToList();

        return new FriendListResponse { Friends = list };
    }

    /// <summary>设置/清除好友备注（note 为空字符串则清除记录）。备注单向，仅本人视角可见。</summary>
    public async Task SetNoteAsync(long userId, long friendUserId, string note)
    {
        if (userId == friendUserId)
        {
            throw new BusinessException(ApiCodes.BadRequest, "不能给自己设置备注");
        }
        await EnsureFriendAsync(userId, friendUserId);

        var existing = await _db.FriendNotes
            .FirstOrDefaultAsync(n => n.UserId == userId && n.FriendId == friendUserId);

        var trimmed = note?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            // 清除备注
            if (existing != null)
            {
                _db.FriendNotes.Remove(existing);
                await _db.SaveChangesAsync();
            }
            return;
        }

        if (existing != null)
        {
            existing.Note = trimmed;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.FriendNotes.Add(new FriendNoteEntity
            {
                UserId = userId,
                FriendId = friendUserId,
                Note = trimmed,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>校验好友关系，非好友抛业务异常。</summary>
    private async Task EnsureFriendAsync(long userId, long friendUserId)
    {
        var isFriend = await _db.Friends.AnyAsync(f =>
            (f.UserId == userId && f.FriendId == friendUserId) ||
            (f.UserId == friendUserId && f.FriendId == userId));
        if (!isFriend)
        {
            throw new BusinessException(ApiCodes.BadRequest, "还不是好友关系");
        }
    }

    /// <summary>删除好友（双向删除）。</summary>
    public async Task DeleteAsync(long userId, long friendUserId)
    {
        var rows = await _db.Friends.Where(f =>
            (f.UserId == userId && f.FriendId == friendUserId) ||
            (f.UserId == friendUserId && f.FriendId == userId)).ToListAsync();
        if (rows.Count == 0)
        {
            throw new BusinessException(ApiCodes.NotFound, "不是好友关系");
        }
        _db.Friends.RemoveRange(rows);
        await _db.SaveChangesAsync();
    }

    /// <summary>待处理的好友申请列表（我是被申请人）。</summary>
    public async Task<FriendRequestListResponse> PendingRequestsAsync(long userId)
    {
        var requests = await _db.FriendRequests
            .Where(r => r.ToUserId == userId && r.Status == "pending")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var fromIds = requests.Select(r => r.FromUserId).Distinct().ToList();
        var users = await _db.Users.Where(u => fromIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u);

        var list = requests.Select(r =>
        {
            users.TryGetValue(r.FromUserId, out var u);
            return new FriendRequestDto
            {
                Id = r.Id,
                FromUserId = r.FromUserId,
                FromNickname = u?.Nickname ?? string.Empty,
                AvatarUrl = u?.AvatarUrl ?? string.Empty,
                Signature = u?.Signature,
                Status = r.Status,
                CreatedAt = new DateTimeOffset(System.DateTime.SpecifyKind(r.CreatedAt, System.DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            };
        }).ToList();

        return new FriendRequestListResponse { Requests = list };
    }

    /// <summary>喂食（占位，文档 08 §8.4）。</summary>
    public Task FeedAsync(long userId, long targetUserId)
    {
        throw new NotImplementedException();
    }
}
