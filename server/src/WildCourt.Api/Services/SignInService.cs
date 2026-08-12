using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 每日签到（文档 02 §2.2 DailySignInController、11 §11.6/§11.7）：
/// 周签到按连续天数 7 天循环领取当日奖励；月签到按累计签到天数领取里程碑奖励
/// （total=14/30，含装饰 deco_month）。
/// 表：sign_in_records（每日一条，user_id+sign_date 唯一防重复签到）。
/// 奖励发放模式对齐 ShopService.BuyAsync：改 user.Shell → 背包 upsert → 记
/// transactions 流水 → 单次 SaveChangesAsync 原子提交（EF 隐式事务）。
/// </summary>
public class SignInService
{
    private const string SignInFile = "Config_DailySignIn.json";
    private const string WeekKind = "week";
    private const string MonthKind = "month";
    private const string ShellCurrency = "shell";

    private readonly WildCourtDbContext _db;
    private readonly ConfigService _config;

    public SignInService(WildCourtDbContext db, ConfigService config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>今日签到状态（GET /today）：今日是否已签 + 连续签到天数。</summary>
    public async Task<SignInTodayResponse> GetTodayAsync(long userId)
    {
        var today8 = TodayUtc8();
        var signedToday = await _db.SignInRecords
            .AnyAsync(r => r.UserId == userId && r.SignDate == today8);
        var days = await CountStreakAsync(userId, signedToday ? today8 : today8.AddDays(-1));
        return new SignInTodayResponse { SignedToday = signedToday, Days = days };
    }

    /// <summary>
    /// 领取周签到（POST /claim-week）：执行今日签到并发放当日周奖励（7 天循环）。
    /// 重复调用（今日已签）幂等返回当前状态，不重复发奖。
    /// </summary>
    public async Task<SignInClaimResponse> ClaimWeekAsync(long userId)
    {
        var today8 = TodayUtc8();

        // 已签：幂等返回，不重复发奖
        var already = await _db.SignInRecords
            .AnyAsync(r => r.UserId == userId && r.SignDate == today8);
        if (already)
        {
            var daysNow = await CountStreakAsync(userId, today8);
            return new SignInClaimResponse { Shell = 0, TotalDays = daysNow };
        }

        var user = await _db.Users.FindAsync(userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "用户不存在");

        // 连续签到天数（含今日）→ 7 天循环定位当日奖励
        var streak = await CountStreakAsync(userId, today8.AddDays(-1));
        var dayOfWeek = (streak % 7) + 1;
        var rows = _config.GetRows<SignInConfigRow>(SignInFile);
        var reward = rows.FirstOrDefault(r =>
            string.Equals(r.Kind, WeekKind, StringComparison.OrdinalIgnoreCase) && r.Day == dayOfWeek);
        if (reward == null)
        {
            throw new BusinessException(ApiCodes.ServerError, $"周签到奖励配置缺失（day={dayOfWeek}）");
        }

        var itemId = string.IsNullOrWhiteSpace(reward.Item) ? null : reward.Item;
        _db.SignInRecords.Add(new SignInRecordEntity
        {
            UserId = userId,
            SignDate = today8,
            CreatedAt = DateTime.UtcNow,
        });
        GrantReward(user, reward.Shell, itemId, reward.Count, reward.Shell, "signin_week", dayOfWeek.ToString());

        await _db.SaveChangesAsync();

        return new SignInClaimResponse
        {
            Shell = reward.Shell,
            ItemId = itemId ?? string.Empty,
            ItemCount = itemId == null ? 0 : reward.Count,
            TotalDays = streak + 1,
        };
    }

    /// <summary>
    /// 领取月签到（POST /claim-month）：按累计签到天数领取全部已达成且未领取的里程碑
    /// （14/30 天），每档仅可领取一次（transactions 查重）。
    /// </summary>
    public async Task<SignInClaimResponse> ClaimMonthAsync(long userId)
    {
        var totalDays = await _db.SignInRecords.CountAsync(r => r.UserId == userId);
        var user = await _db.Users.FindAsync(userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "用户不存在");

        var rows = _config.GetRows<SignInConfigRow>(SignInFile);
        var milestones = rows
            .Where(r => string.Equals(r.Kind, MonthKind, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Total)
            .ToList();
        if (milestones.Count == 0)
        {
            throw new BusinessException(ApiCodes.ServerError, "月签到里程碑配置缺失");
        }

        var response = new SignInClaimResponse { TotalDays = totalDays };
        var changed = false;

        foreach (var m in milestones)
        {
            if (totalDays < m.Total)
            {
                continue; // 未达成
            }
            // 防重复领取：transactions 查重
            var key = $"milestone_{m.Total}";
            var claimed = await _db.Transactions
                .AnyAsync(t => t.UserId == userId && t.Type == "signin_month" && t.ItemId == key);
            if (claimed)
            {
                continue;
            }

            var itemId = string.IsNullOrWhiteSpace(m.Item) ? null : m.Item;
            GrantReward(user, m.Shell, itemId, m.Count, m.Shell, "signin_month", key);
            response.Shell += m.Shell;
            if (itemId != null)
            {
                response.ItemId = itemId;
                response.ItemCount += m.Count;
            }
            changed = true;
        }

        if (!changed)
        {
            throw new BusinessException(ApiCodes.BadRequest, "暂无满足条件的月签到奖励可领取");
        }

        await _db.SaveChangesAsync();
        return response;
    }

    /// <summary>UTC+8 自然日零点（与客户端展示/结算时区对齐）。</summary>
    private static DateTime TodayUtc8() => DateTime.UtcNow.AddHours(8).Date;

    /// <summary>从锚点日期向前统计连续签到天数。</summary>
    private async Task<int> CountStreakAsync(long userId, DateTime anchor)
    {
        var dates = await _db.SignInRecords
            .Where(r => r.UserId == userId)
            .Select(r => r.SignDate)
            .ToHashSetAsync();
        var cursor = anchor;
        var streak = 0;
        while (dates.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }

    /// <summary>发放奖励：庭贝 + 可选道具 → 背包 upsert + transactions 流水。</summary>
    private void GrantReward(UserEntity user, int shell, string? itemId, int itemCount, int price, string type, string refId)
    {
        if (shell > 0)
        {
            user.Shell += shell;
            _db.Transactions.Add(new TransactionEntity
            {
                UserId = user.UserId,
                Type = type,
                ItemId = ShellCurrency,
                Count = shell,
                Price = 0,
            });
        }

        if (string.IsNullOrWhiteSpace(itemId) || itemCount <= 0)
        {
            return;
        }

        var inventory = _db.Inventories.Local.FirstOrDefault(i => i.UserId == user.UserId && i.ItemId == itemId);
        if (inventory == null)
        {
            inventory = _db.Inventories.FirstOrDefault(i => i.UserId == user.UserId && i.ItemId == itemId);
        }
        if (inventory == null)
        {
            _db.Inventories.Add(new InventoryEntity { UserId = user.UserId, ItemId = itemId, Count = itemCount });
        }
        else
        {
            inventory.Count += itemCount;
        }

        _db.Transactions.Add(new TransactionEntity
        {
            UserId = user.UserId,
            Type = type,
            ItemId = itemId,
            Count = itemCount,
            Price = 0,
        });
    }
}
