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

    /// <summary>好友申请（占位）。</summary>
    public Task ApplyAsync(long fromUserId, long toUserId)
    {
        throw new NotImplementedException();
    }

    /// <summary>喂食（占位，文档 08 §8.4）。</summary>
    public Task FeedAsync(long userId, long targetUserId)
    {
        throw new NotImplementedException();
    }
}
