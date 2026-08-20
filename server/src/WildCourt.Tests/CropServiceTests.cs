using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Api.Services;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;
using Xunit;

namespace WildCourt.Tests;

/// <summary>
/// 种田业务测试（文档 04）：种植校验（作物/格子/占用/过期覆盖/结束时间）、状态查询。
/// 使用 InMemory DbContext + 临时目录 JSON 配置（ConfigService 读文件），与 YardServiceTests 同模式。
/// </summary>
public class CropServiceTests : IDisposable
{
    private readonly string _configDir;
    private readonly WildCourtDbContext _db;
    private readonly CropService _service;
    private const long UserId = 1;

    public CropServiceTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "wc_crop_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDir);
        // 容量：farmlandCount=6（格子 0~5）
        File.WriteAllText(Path.Combine(_configDir, "Config_YardUnlock.json"),
            "{\"rows\":[{\"level\":1,\"farmlandCount\":6,\"processorSlots\":2,\"foodBowlCapacity\":10,\"decorationLimit\":3,\"showcaseSlots\":3,\"mailboxCapacity\":20,\"unlockCond\":\"Lv1\"}]}");
        File.WriteAllText(Path.Combine(_configDir, "Config_Crops.json"),
            "{\"rows\":[" +
            "{\"id\":\"C01\",\"name\":\"小麦\",\"growTime\":20,\"yield\":12,\"sellPrice\":5,\"processTo\":\"面粉\",\"processTime\":3}," +
            "{\"id\":\"C02\",\"name\":\"胡萝卜\",\"growTime\":30,\"yield\":8,\"sellPrice\":8,\"processTo\":\"\",\"processTime\":0}," +
            "{\"id\":\"C03\",\"name\":\"棉花\",\"growTime\":40,\"yield\":8,\"sellPrice\":12,\"processTo\":\"丝绸\",\"processTime\":8}" +
            "]}");
        File.WriteAllText(Path.Combine(_configDir, "Config_Shop.json"),
            "{\"rows\":[" +
            "{\"id\":\"seed_wheat\",\"itemName\":\"小麦种子\",\"category\":\"seed\",\"buyPrice\":2,\"sellPrice\":0,\"unlockCond\":\"商店\",\"effect\":\"\",\"cropId\":\"C01\"}," +
            "{\"id\":\"seed_carrot\",\"itemName\":\"胡萝卜种子\",\"category\":\"seed\",\"buyPrice\":5,\"sellPrice\":0,\"unlockCond\":\"商店\",\"effect\":\"\",\"cropId\":\"C02\"}," +
            "{\"id\":\"seed_cotton\",\"itemName\":\"棉花种子\",\"category\":\"seed\",\"buyPrice\":8,\"sellPrice\":0,\"unlockCond\":\"商店\",\"effect\":\"\",\"cropId\":\"C03\"}" +
            "]}");

        var options = new DbContextOptionsBuilder<WildCourtDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        _db = new WildCourtDbContext(options);
        _db.Users.Add(new UserEntity
        {
            UserId = UserId,
            Username = "tester",
            PasswordHash = "x",
            Nickname = "t",
            Level = 1,
            CreatedAt = DateTime.UtcNow,
        });
        _db.Inventories.AddRange(
            new InventoryEntity { UserId = UserId, ItemId = "seed_wheat", Count = 5 },
            new InventoryEntity { UserId = UserId, ItemId = "seed_carrot", Count = 1 });
        _db.SaveChanges();

        _service = new CropService(_db, new ConfigService(_configDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_configDir, recursive: true); } catch { /* 清理失败忽略 */ }
    }

    [Fact]
    public async Task Plant_ValidCrop_StoresPlot()
    {
        var plot = await _service.PlantAsync(UserId, 0, "C01");

        Assert.Equal(0, plot.CellId);
        Assert.Equal("C01", plot.CropId);
        Assert.Equal("growing", plot.State);
        Assert.Single(_db.FarmPlots.Where(p => p.UserId == UserId));
    }

    [Fact]
    public async Task Plant_UnknownCrop_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PlantAsync(UserId, 0, "X99"));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("作物不存在", ex.Message);
    }

    [Fact]
    public async Task Plant_CellOutOfRange_Throws()
    {
        // farmlandCount=6，合法格子 0~5
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PlantAsync(UserId, 6, "C01"));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("农田格越界", ex.Message);
    }

    [Fact]
    public async Task Plant_CellOccupied_Throws()
    {
        await _service.PlantAsync(UserId, 0, "C01");

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PlantAsync(UserId, 0, "C02"));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("已有作物在生长", ex.Message);
    }

    [Fact]
    public async Task Plant_ExpiredCell_AllowsReplant()
    {
        await _service.PlantAsync(UserId, 0, "C01");
        var row = await _db.FarmPlots.SingleAsync(p => p.UserId == UserId && p.CellId == 0);
        row.ReadyAt = DateTime.UtcNow.AddMinutes(-1); // 模拟已过期（v1 无收获接口）
        await _db.SaveChangesAsync();

        var plot = await _service.PlantAsync(UserId, 0, "C02");

        Assert.Equal("C02", plot.CropId);
        Assert.Single(_db.FarmPlots.Where(p => p.UserId == UserId));
    }

    [Fact]
    public async Task Plant_ServerComputesReadyAt()
    {
        // 结束时间由服务端按自身时钟权威计算（不信任客户端时钟，避免时钟偏差导致成熟时间不一致）
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var plot = await _service.PlantAsync(UserId, 0, "C01"); // growTime 20 分钟

        // readyAt = 服务端 now + growTime*60（C01 = 20 分钟），误差仅本次调用耗时（<2s）
        Assert.InRange(plot.ReadyAt, before + 20 * 60L, before + 20 * 60L + 2);
        var row = await _db.FarmPlots.SingleAsync(p => p.UserId == UserId && p.CellId == 0);
        Assert.Equal(plot.ReadyAt, new DateTimeOffset(row.ReadyAt.ToUniversalTime()).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task GetPlots_MarksExpiredAsMature()
    {
        var future = DateTime.UtcNow.AddMinutes(10);
        var past = DateTime.UtcNow.AddMinutes(-1);
        _db.FarmPlots.AddRange(
            new FarmPlotEntity { UserId = UserId, CellId = 0, CropId = "C01", PlantedAt = DateTime.UtcNow, ReadyAt = past, State = "growing", UpdatedAt = DateTime.UtcNow },
            new FarmPlotEntity { UserId = UserId, CellId = 1, CropId = "C02", PlantedAt = DateTime.UtcNow, ReadyAt = future, State = "growing", UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var resp = await _service.GetPlotsAsync(UserId);

        Assert.Equal(2, resp.Plots.Count);
        Assert.Equal("mature", resp.Plots[0].State);  // cell 0 已过期
        Assert.Equal("growing", resp.Plots[1].State); // cell 1 未到期
        Assert.True(resp.ServerTime > 0);             // 客户端用 readyAt - serverTime 消除时钟偏差
    }

    [Fact]
    public async Task Plant_WithoutSeed_Throws()
    {
        // C03 棉花：玩家未持有 seed_cotton
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PlantAsync(UserId, 0, "C03"));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("种子不足", ex.Message);
        Assert.Empty(_db.FarmPlots);
    }

    [Fact]
    public async Task Plant_WithSeed_DeductsOneAndRecordsTransaction()
    {
        await _service.PlantAsync(UserId, 0, "C01");

        var inv = await _db.Inventories.SingleAsync(i => i.UserId == UserId && i.ItemId == "seed_wheat");
        Assert.Equal(4, inv.Count); // 5 - 1

        var tx = Assert.Single(_db.Transactions);
        Assert.Equal("plant", tx.Type);
        Assert.Equal("seed_wheat", tx.ItemId);
        Assert.Equal(-1, tx.Count);
        Assert.Equal(0, tx.Price);
    }

    [Fact]
    public async Task Plant_SeedCountZero_Throws()
    {
        var inv = await _db.Inventories.SingleAsync(i => i.UserId == UserId && i.ItemId == "seed_carrot");
        inv.Count = 0;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PlantAsync(UserId, 0, "C02"));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("种子不足", ex.Message);
        Assert.Empty(_db.FarmPlots);
    }

    [Fact]
    public async Task Harvest_NoPlot_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.HarvestAsync(UserId, 3));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("没有作物", ex.Message);
    }

    [Fact]
    public async Task Harvest_NotMature_Throws()
    {
        _db.FarmPlots.Add(new FarmPlotEntity
        {
            UserId = UserId,
            CellId = 0,
            CropId = "C01",
            PlantedAt = DateTime.UtcNow,
            ReadyAt = DateTime.UtcNow.AddMinutes(10),
            State = "growing",
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.HarvestAsync(UserId, 0));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("尚未成熟", ex.Message);
    }

    [Fact]
    public async Task Harvest_Mature_AddsYieldAndClearsPlot()
    {
        _db.FarmPlots.Add(new FarmPlotEntity
        {
            UserId = UserId,
            CellId = 0,
            CropId = "C01",
            PlantedAt = DateTime.UtcNow.AddMinutes(-30),
            ReadyAt = DateTime.UtcNow.AddMinutes(-1), // 已成熟
            State = "growing",
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var result = await _service.HarvestAsync(UserId, 0);

        Assert.Equal(0, result.CellId);
        Assert.Equal("C01", result.CropId);
        Assert.Equal(12, result.Count); // 测试配置 C01 yield=12

        var inv = await _db.Inventories.SingleAsync(i => i.UserId == UserId && i.ItemId == "C01");
        Assert.Equal(12, inv.Count);
        Assert.Empty(_db.FarmPlots.Where(p => p.UserId == UserId));

        var tx = Assert.Single(_db.Transactions);
        Assert.Equal("harvest", tx.Type);
        Assert.Equal("C01", tx.ItemId);
        Assert.Equal(12, tx.Count);
        Assert.Equal(0, tx.Price);
    }

    [Fact]
    public async Task Harvest_AfterSteal_ReturnsRemaining()
    {
        _db.FarmPlots.Add(new FarmPlotEntity
        {
            UserId = UserId,
            CellId = 0,
            CropId = "C01",
            PlantedAt = DateTime.UtcNow.AddMinutes(-30),
            ReadyAt = DateTime.UtcNow.AddMinutes(-1),
            State = "growing",
            StolenCount = 3, // 已被好友偷 3 个
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var result = await _service.HarvestAsync(UserId, 0);

        Assert.Equal(9, result.Count); // 12 - 3
        var inv = await _db.Inventories.SingleAsync(i => i.UserId == UserId && i.ItemId == "C01");
        Assert.Equal(9, inv.Count);
    }

    [Fact]
    public async Task Harvest_AllStolen_Throws()
    {
        _db.FarmPlots.Add(new FarmPlotEntity
        {
            UserId = UserId,
            CellId = 0,
            CropId = "C01",
            PlantedAt = DateTime.UtcNow.AddMinutes(-30),
            ReadyAt = DateTime.UtcNow.AddMinutes(-1),
            State = "growing",
            StolenCount = 12, // 全被偷光
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.HarvestAsync(UserId, 0));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("已被偷光", ex.Message);
    }
}
