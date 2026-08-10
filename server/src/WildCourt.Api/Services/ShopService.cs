using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 商店业务（阶段 B，文档 02 §2.3、09）：装饰 + 种子货架与庭贝购买。
/// 材料/道具/鱼饵等货架待对应配置表导出后补充。
/// </summary>
public class ShopService
{
    private const string DecorationsFile = "Config_Decorations.json";
    private const string ShopFile = "Config_Shop.json";
    private const string ShopAcquire = "商店";
    private const string ShellCurrency = "shell";
    private const string SeedCategory = "seed";

    private readonly WildCourtDbContext _db;
    private readonly ConfigService _config;

    public ShopService(WildCourtDbContext db, ConfigService config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>货架列表：商店来源装饰（文档 09 §9.6）+ 种子（Config_Shop.json，§9.4）。</summary>
    public Task<List<ShelfItem>> GetShelvesAsync()
    {
        var rows = _config.GetRows<DecorationConfig>(DecorationsFile);
        var decorations = rows
            .Where(d => d.Acquire == ShopAcquire)
            .OrderBy(d => d.Id)
            .Select(d => new ShelfItem
            {
                Id = d.Id,
                ItemName = d.Name,
                Category = d.Category,
                BuyPrice = d.Price,
                SellPrice = 0, // 装饰删除不返还庭贝（文档 03 §3.9），不设回收价
                UnlockCond = d.Acquire,
                Effect = d.Description,
            })
            .ToList();
        var seeds = _config.GetRows<ShopItemConfigRow>(ShopFile)
            .Where(s => string.Equals(s.Category, SeedCategory, StringComparison.OrdinalIgnoreCase) && s.BuyPrice > 0)
            .OrderBy(s => s.Id)
            .Select(s => new ShelfItem
            {
                Id = s.Id,
                ItemName = s.ItemName,
                Category = s.Category,
                BuyPrice = s.BuyPrice,
                SellPrice = s.SellPrice,
                UnlockCond = s.UnlockCond,
                Effect = s.Effect,
            });
        return Task.FromResult(decorations.Concat(seeds).ToList());
    }

    /// <summary>购买（装饰/种子）：扣庭贝 → 背包 upsert → 记 transactions。
    /// 三个变更在一次 SaveChangesAsync 内原子提交（EF 隐式事务）；并发扣减行锁方案按文档 04 §4.5 留待经济系统阶段。</summary>
    public async Task<BuyResponse> BuyAsync(long userId, string itemId, int count, string currency)
    {
        if (count <= 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "购买数量必须大于 0");
        }
        if (!string.Equals(currency, ShellCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ApiCodes.BadRequest, "商品仅支持庭贝购买");
        }

        var decorations = _config.GetRows<DecorationConfig>(DecorationsFile);
        var deco = decorations.FirstOrDefault(d => string.Equals(d.Id, itemId, StringComparison.OrdinalIgnoreCase));
        var seed = _config.GetRows<ShopItemConfigRow>(ShopFile)
            .FirstOrDefault(s => string.Equals(s.Id, itemId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.Category, SeedCategory, StringComparison.OrdinalIgnoreCase));

        int pricePerUnit;
        int maxCount = int.MaxValue; // 种子无上限；装饰按配置 maxCount
        if (deco != null && deco.Acquire == ShopAcquire)
        {
            pricePerUnit = deco.Price;
            maxCount = deco.MaxCount;
        }
        else if (seed != null)
        {
            pricePerUnit = seed.BuyPrice;
        }
        else
        {
            throw new BusinessException(ApiCodes.BadRequest, "商品不存在或不可购买");
        }

        var inventory = await _db.Inventories.FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId);
        int ownedNow = inventory?.Count ?? 0;
        if (ownedNow + count > maxCount)
        {
            throw new BusinessException(ApiCodes.BadRequest, $"已达到该商品数量上限（{maxCount}）");
        }

        var total = pricePerUnit * count;
        var user = await _db.Users.FindAsync(userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "用户不存在");
        if (user.Shell < total)
        {
            throw new BusinessException(ApiCodes.BadRequest, "庭贝不足");
        }

        user.Shell -= total;

        if (inventory == null)
        {
            _db.Inventories.Add(new InventoryEntity { UserId = userId, ItemId = itemId, Count = count });
        }
        else
        {
            inventory.Count += count;
        }

        _db.Transactions.Add(new TransactionEntity
        {
            UserId = userId,
            Type = "shop",
            ItemId = itemId,
            Count = count,
            Price = total,
        });

        await _db.SaveChangesAsync();

        return new BuyResponse
        {
            Shell = user.Shell,
            Coupon = user.Coupon,
            ItemId = itemId,
            Count = count,
        };
    }
}
