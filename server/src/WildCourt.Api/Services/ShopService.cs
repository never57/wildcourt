using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 商店业务（阶段 B 最小实现，文档 02 §2.3、09）：装饰货架 + 庭贝购买。
/// 种子/材料/道具/鱼饵等货架待对应配置表导出后补充。
/// </summary>
public class ShopService
{
    private const string DecorationsFile = "Config_Decorations.json";
    private const string ShopAcquire = "商店";
    private const string ShellCurrency = "shell";

    private readonly WildCourtDbContext _db;
    private readonly ConfigService _config;

    public ShopService(WildCourtDbContext db, ConfigService config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>货架列表：仅返回“商店”来源的装饰（Config_Decorations.acquire == 商店，文档 09 §9.6）。</summary>
    public Task<List<ShelfItem>> GetShelvesAsync()
    {
        var rows = _config.GetRows<DecorationConfig>(DecorationsFile);
        var items = rows
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
        return Task.FromResult(items);
    }

    /// <summary>购买装饰：扣庭贝 → 背包 upsert → 记 transactions。
    /// 三个变更在一次 SaveChangesAsync 内原子提交（EF 隐式事务）；并发扣减行锁方案按文档 04 §4.5 留待经济系统阶段。</summary>
    public async Task<BuyResponse> BuyAsync(long userId, string itemId, int count, string currency)
    {
        if (count <= 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "购买数量必须大于 0");
        }
        if (!string.Equals(currency, ShellCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ApiCodes.BadRequest, "装饰仅支持庭贝购买");
        }

        var decorations = _config.GetRows<DecorationConfig>(DecorationsFile);
        var deco = decorations.FirstOrDefault(d => string.Equals(d.Id, itemId, StringComparison.OrdinalIgnoreCase));
        if (deco == null || deco.Acquire != ShopAcquire)
        {
            throw new BusinessException(ApiCodes.BadRequest, "商品不存在或不可购买");
        }

        var inventory = await _db.Inventories.FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId);
        int ownedNow = inventory?.Count ?? 0;
        if (ownedNow + count > deco.MaxCount)
        {
            throw new BusinessException(ApiCodes.BadRequest, $"已达到该装饰数量上限（{deco.MaxCount}）");
        }

        var total = deco.Price * count;
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
