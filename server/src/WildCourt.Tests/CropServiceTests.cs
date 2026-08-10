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
            "{\"id\":\"C01\",\"name\":\"小麦\",\"growTime\":5,\"yield\":2,\"sellPrice\":8,\"processTo\":\"\",\"processTime\":0}," +
            "{\"id\":\"C02\",\"name\":\"玉米\",\"growTime\":10,\"yield\":2,\"sellPrice\":12,\"processTo\":\"\",\"processTime\":0}" +
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
        _db.SaveChanges();

        _service = new CropService(_db, new ConfigService(_configDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_configDir, recursive: true); } catch { /* 清理失败忽略 */ }
    }

    private static long FutureReadyAt(int minutes) =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds() + minutes * 60L;

    [Fact]
    public async Task Plant_ValidCrop_StoresPlot()
    {
        var plot = await _service.PlantAsync(UserId, 0, "C01", FutureReadyAt(5));

        Assert.Equal(0, plot.CellId);
        Assert.Equal("C01", plot.CropId);
        Assert.Equal("growing", plot.State);
        Assert.Single(_db.FarmPlots.Where(p => p.UserId == UserId));
    }

    [Fact]
    public async Task Plant_UnknownCrop_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PlantAsync(UserId, 0, "X99", FutureReadyAt(5)));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("作物不存在", ex.Message);
    }

    [Fact]
    public async Task Plant_CellOutOfRange_Throws()
    {
        // farmlandCount=6，合法格子 0~5
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PlantAsync(UserId, 6, "C01", FutureReadyAt(5)));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("农田格越界", ex.Message);
    }

    [Fact]
    public async Task Plant_CellOccupied_Throws()
    {
        await _service.PlantAsync(UserId, 0, "C01", FutureReadyAt(5));

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PlantAsync(UserId, 0, "C02", FutureReadyAt(10)));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("已有作物在生长", ex.Message);
    }

    [Fact]
    public async Task Plant_ExpiredCell_AllowsReplant()
    {
        await _service.PlantAsync(UserId, 0, "C01", FutureReadyAt(5));
        var row = await _db.FarmPlots.SingleAsync(p => p.UserId == UserId && p.CellId == 0);
        row.ReadyAt = DateTime.UtcNow.AddMinutes(-1); // 模拟已过期（v1 无收获接口）
        await _db.SaveChangesAsync();

        var plot = await _service.PlantAsync(UserId, 0, "C02", FutureReadyAt(10));

        Assert.Equal("C02", plot.CropId);
        Assert.Single(_db.FarmPlots.Where(p => p.UserId == UserId));
    }

    [Fact]
    public async Task Plant_PastReadyAt_Throws()
    {
        var past = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60;

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PlantAsync(UserId, 0, "C01", past));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("结束时间", ex.Message);
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
    }
}
