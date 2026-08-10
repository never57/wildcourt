using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 种田业务（文档 04）：种植上报（客户端 Lua 识别作物并计算结束时间后 POST）、
/// 种植状态查询、成熟收获（产量入背包并清空格子）。v1 信任客户端上报的 readyAt
/// （仅做合法性校验），反作弊重算 / 偷菜结算留后续。
/// </summary>
public class CropService
{
    private const string CropsFile = "Config_Crops.json";
    private const string ShopFile = "Config_Shop.json";
    private const string YardUnlockFile = "Config_YardUnlock.json";
    private const string GrowingState = "growing";
    private const string MatureState = "mature";
    private const string SeedCategory = "seed";

    private readonly WildCourtDbContext _db;
    private readonly ConfigService _config;

    public CropService(WildCourtDbContext db, ConfigService config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// 种植：校验作物存在、格子范围（玩家等级 farmlandCount）、结束时间合法、格子未占用，
    /// 已过期格允许覆盖重种（v1 无收获接口，避免格子卡死）。
    /// </summary>
    public async Task<FarmPlotDto> PlantAsync(long userId, int cellId, string cropId, long readyAt)
    {
        if (string.IsNullOrWhiteSpace(cropId))
        {
            throw new BusinessException(ApiCodes.BadRequest, "作物 ID 不能为空");
        }

        var crop = _config.GetRows<CropConfigRow>(CropsFile)
            .FirstOrDefault(c => string.Equals(c.Id, cropId, StringComparison.OrdinalIgnoreCase));
        if (crop == null)
        {
            throw new BusinessException(ApiCodes.BadRequest, $"作物不存在: {cropId}");
        }

        var user = await _db.Users.FindAsync(userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "用户不存在");
        var unlock = GetUnlockConfig(user.Level);
        if (cellId < 0 || cellId >= unlock.FarmlandCount)
        {
            throw new BusinessException(ApiCodes.BadRequest, $"农田格越界（0~{unlock.FarmlandCount - 1}）");
        }

        var now = DateTimeOffset.UtcNow;
        if (readyAt <= now.ToUnixTimeSeconds())
        {
            throw new BusinessException(ApiCodes.BadRequest, "作物结束时间必须晚于当前时间");
        }

        var existing = await _db.FarmPlots.FindAsync(userId, cellId);
        if (existing != null && existing.ReadyAt > now.UtcDateTime)
        {
            throw new BusinessException(ApiCodes.BadRequest, "该农田格已有作物在生长");
        }

        // 种子校验：按作物查商店配置（Config_Shop.json），有种子映射则必须持有 ≥1 个
        var seed = _config.GetRows<ShopItemConfigRow>(ShopFile)
            .FirstOrDefault(s => string.Equals(s.CropId, crop.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.Category, SeedCategory, StringComparison.OrdinalIgnoreCase));
        InventoryEntity? seedInventory = null;
        if (seed != null)
        {
            seedInventory = await _db.Inventories.FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == seed.Id);
            if (seedInventory == null || seedInventory.Count <= 0)
            {
                throw new BusinessException(ApiCodes.BadRequest, $"种子不足（需要 {seed.ItemName}）");
            }
        }

        var ready = DateTimeOffset.FromUnixTimeSeconds(readyAt).UtcDateTime;
        if (existing == null)
        {
            _db.FarmPlots.Add(new FarmPlotEntity
            {
                UserId = userId,
                CellId = cellId,
                CropId = crop.Id,
                PlantedAt = now.UtcDateTime,
                ReadyAt = ready,
                State = GrowingState,
                UpdatedAt = now.UtcDateTime,
            });
        }
        else
        {
            existing.CropId = crop.Id;
            existing.PlantedAt = now.UtcDateTime;
            existing.ReadyAt = ready;
            existing.State = GrowingState;
            existing.UpdatedAt = now.UtcDateTime;
        }

        // 消耗 1 颗种子并记账，与种植写入同一次 SaveChangesAsync 原子提交
        if (seed != null && seedInventory != null)
        {
            seedInventory.Count -= 1;
            _db.Transactions.Add(new TransactionEntity
            {
                UserId = userId,
                Type = "plant",
                ItemId = seed.Id,
                Count = -1, // 负值表示消耗
                Price = 0,
            });
        }
        await _db.SaveChangesAsync();

        return new FarmPlotDto
        {
            CellId = cellId,
            CropId = crop.Id,
            ReadyAt = readyAt,
            State = GrowingState,
        };
    }

    /// <summary>种植状态列表；readyAt 已过的标记 mature（查询时计算，不落库）。</summary>
    public async Task<FarmPlotsResponse> GetPlotsAsync(long userId)
    {
        var nowUtc = DateTime.UtcNow;
        var rows = await _db.FarmPlots
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CellId)
            .ToListAsync();

        var plots = rows.Select(p => new FarmPlotDto
        {
            CellId = p.CellId,
            CropId = p.CropId,
            ReadyAt = new DateTimeOffset(p.ReadyAt.ToUniversalTime()).ToUnixTimeSeconds(),
            State = p.ReadyAt <= nowUtc ? MatureState : GrowingState,
        }).ToList();

        return new FarmPlotsResponse { Plots = plots };
    }

    /// <summary>
    /// 收获：成熟（readyAt 已过）作物产量入背包并删除种植记录（格子立即可重种），
    /// 与流水同一次 SaveChangesAsync 原子提交。
    /// </summary>
    public async Task<HarvestResultDto> HarvestAsync(long userId, int cellId)
    {
        var plot = await _db.FarmPlots.FindAsync(userId, cellId)
            ?? throw new BusinessException(ApiCodes.BadRequest, "该农田格没有作物");
        if (plot.ReadyAt > DateTime.UtcNow)
        {
            throw new BusinessException(ApiCodes.BadRequest, "作物尚未成熟");
        }

        var crop = _config.GetRows<CropConfigRow>(CropsFile)
            .FirstOrDefault(c => string.Equals(c.Id, plot.CropId, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessException(ApiCodes.BadRequest, $"作物配置缺失: {plot.CropId}");

        var inventory = await _db.Inventories.FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == crop.Id);
        if (inventory == null)
        {
            _db.Inventories.Add(new InventoryEntity { UserId = userId, ItemId = crop.Id, Count = crop.Yield });
        }
        else
        {
            inventory.Count += crop.Yield;
        }

        _db.Transactions.Add(new TransactionEntity
        {
            UserId = userId,
            Type = "harvest",
            ItemId = crop.Id,
            Count = crop.Yield,
            Price = 0,
        });

        _db.FarmPlots.Remove(plot);
        await _db.SaveChangesAsync();

        return new HarvestResultDto { CellId = cellId, CropId = crop.Id, Count = crop.Yield };
    }

    /// <summary>按玩家等级取庭院/农田容量（Config_YardUnlock.json；查不到按满级兜底）。</summary>
    private YardUnlockConfig GetUnlockConfig(int level)
    {
        var rows = _config.GetRows<YardUnlockConfig>(YardUnlockFile);
        return rows.FirstOrDefault(r => r.Level == level)
            ?? rows.OrderByDescending(r => r.Level).FirstOrDefault()
            ?? new YardUnlockConfig { Level = level };
    }
}
