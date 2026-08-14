using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 信箱业务（仅官方信息发放链路）：
/// 玩家间写信（Send/GiftBack/审核投递）已剥离至独立的写信系统，本服务不再提供。
/// 本服务只负责：官方发信（由 AdminService 写库）→ 玩家收件箱 → 领取附件 → 删除已读。
///
/// 附件类型（reward_type）：
///   shell  → 庭贝（直接改 user.Shell）
///   coupon → 邮券（直接改 user.Coupon）
///   item   → 背包道具/非遗（inventories 表 upsert）
///   空      → 无附件
///
/// 状态约定（复用 letters.status 字段，官方链路不经过审核）：
///   "未读"（官方信件初始状态，点击后转已读）
///   "已读"（玩家点击阅读过，可领取附件/可删除）
///   "已领取"（附件已发放，仅可删除；领取幂等标记）
///   "已删除"（软删除标记，收件箱不再返回；物理删除留待清理任务）
/// 状态机：未读 →（点击）已读 →（领取附件）已领取；未读/已读/已领取 →（删除）已删除
/// </summary>
public class LetterService
{
    private const string StatusUnread = "未读";
    private const string StatusRead = "已读";
    private const string StatusClaimed = "已领取";
    private const string StatusDeleted = "已删除";

    private const string RewardShell = "shell";
    private const string RewardCoupon = "coupon";
    private const string RewardItem = "item";

    private const string LetterClaimType = "letter_claim";

    private readonly WildCourtDbContext _db;

    public LetterService(WildCourtDbContext db)
    {
        _db = db;
    }

    /// <summary>收件箱：当前用户的官方信件（排除已删除），按时间倒序。</summary>
    public async Task<InboxResponse> InboxAsync(long userId)
    {
        var letters = await _db.Letters
            .Where(l => l.ToUser == userId && l.Status != StatusDeleted)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        var fromIds = letters
            .Select(l => l.FromUser)
            .Where(id => id != 0)
            .Distinct()
            .ToList();
        var nicknames = fromIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.Users
                .Where(u => fromIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.Nickname);

        return new InboxResponse
        {
            Letters = letters.Select(l => new Letter
            {
                LetterId = l.LetterId,
                FromUserId = l.FromUser,
                FromNickname = l.FromUser == 0 ? "官方" : (nicknames.TryGetValue(l.FromUser, out var n) ? n : ""),
                ToUserId = l.ToUser,
                Title = l.Title ?? string.Empty,
                Content = l.Content,
                ItemId = l.ItemId,
                RewardType = l.RewardType,
                RewardCount = l.RewardCount,
                Status = l.Status,
                CreatedAt = new DateTimeOffset(l.CreatedAt).ToUnixTimeMilliseconds(),
            }).ToList(),
        };
    }

    /// <summary>标记信件已读（未读 → 已读，幂等：已读/已领取不重复操作）。</summary>
    public async Task MarkReadAsync(long userId, long letterId)
    {
        var letter = await _db.Letters.FirstOrDefaultAsync(l => l.LetterId == letterId)
            ?? throw new BusinessException(ApiCodes.NotFound, "信件不存在");
        if (letter.ToUser != userId)
        {
            throw new BusinessException(ApiCodes.Forbidden, "无权操作该信件");
        }
        if (letter.Status == StatusUnread)
        {
            letter.Status = StatusRead;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>领取邮件附件（按 reward_type 分派发放），幂等防重复。</summary>
    public async Task<ClaimResponse> ClaimAsync(long userId, long letterId)
    {
        var letter = await _db.Letters.FirstOrDefaultAsync(l => l.LetterId == letterId)
            ?? throw new BusinessException(ApiCodes.NotFound, "信件不存在");
        if (letter.ToUser != userId)
        {
            throw new BusinessException(ApiCodes.Forbidden, "无权操作该信件");
        }
        if (letter.Status == StatusDeleted)
        {
            throw new BusinessException(ApiCodes.NotFound, "信件不存在");
        }

        var rewardType = string.IsNullOrWhiteSpace(letter.RewardType) ? null : letter.RewardType;
        if (letter.Status == StatusClaimed)
        {
            // 幂等：已领取直接返回，不重复发奖
            return new ClaimResponse
            {
                LetterId = letterId,
                RewardType = rewardType ?? string.Empty,
                ItemId = rewardType == RewardItem ? (letter.ItemId ?? string.Empty) : string.Empty,
                Count = letter.RewardCount,
            };
        }

        if (rewardType != null)
        {
            var user = await _db.Users.FindAsync(userId)
                ?? throw new BusinessException(ApiCodes.NotFound, "用户不存在");
            GrantReward(user, letter, rewardType);
        }

        letter.Status = StatusClaimed;
        await _db.SaveChangesAsync();

        return new ClaimResponse
        {
            LetterId = letterId,
            RewardType = rewardType ?? string.Empty,
            ItemId = rewardType == RewardItem ? (letter.ItemId ?? string.Empty) : string.Empty,
            Count = letter.RewardCount,
        };
    }

    /// <summary>删除已读/已领取信件（软删除，仅归属本人可删）。</summary>
    public async Task DeleteAsync(long userId, long letterId)
    {
        var letter = await _db.Letters.FirstOrDefaultAsync(l => l.LetterId == letterId)
            ?? throw new BusinessException(ApiCodes.NotFound, "信件不存在");
        if (letter.ToUser != userId)
        {
            throw new BusinessException(ApiCodes.Forbidden, "无权操作该信件");
        }
        if (letter.Status == StatusDeleted)
        {
            return; // 幂等
        }

        letter.Status = StatusDeleted;
        await _db.SaveChangesAsync();
    }

    /// <summary>按附件类型分派发放（对齐 SignInService/ShopService 模式）。</summary>
    private void GrantReward(UserEntity user, LetterEntity letter, string rewardType)
    {
        var count = letter.RewardCount;
        if (count <= 0)
        {
            return;
        }

        if (rewardType == RewardShell)
        {
            user.Shell += count;
            AddTransaction(user.UserId, RewardShell, count);
        }
        else if (rewardType == RewardCoupon)
        {
            user.Coupon += count;
            AddTransaction(user.UserId, RewardCoupon, count);
        }
        else if (rewardType == RewardItem)
        {
            var itemId = letter.ItemId;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new BusinessException(ApiCodes.ServerError, "附件道具 ID 缺失");
            }

            var inventory = _db.Inventories.Local.FirstOrDefault(i => i.UserId == user.UserId && i.ItemId == itemId)
                ?? _db.Inventories.FirstOrDefault(i => i.UserId == user.UserId && i.ItemId == itemId);
            if (inventory == null)
            {
                _db.Inventories.Add(new InventoryEntity { UserId = user.UserId, ItemId = itemId, Count = count });
            }
            else
            {
                inventory.Count += count;
            }
            AddTransaction(user.UserId, itemId, count);
        }
        else
        {
            throw new BusinessException(ApiCodes.ServerError, $"未知附件类型：{rewardType}");
        }
    }

    private void AddTransaction(long userId, string itemId, int count)
    {
        _db.Transactions.Add(new TransactionEntity
        {
            UserId = userId,
            Type = LetterClaimType,
            ItemId = itemId,
            Count = count,
            Price = 0,
        });
    }
}
