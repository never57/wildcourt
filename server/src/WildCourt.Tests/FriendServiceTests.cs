using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Api.Services;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;
using Xunit;

namespace WildCourt.Tests;

/// <summary>
/// 好友服务测试（文档 08 §8.3）：偷菜成功/数量、自己/非好友/未成熟拒绝、每日限额、已偷光。
/// 使用 InMemory DbContext + 临时目录 JSON 配置（ConfigService 读文件）。
/// </summary>
public class FriendServiceTests : IDisposable
{
    private readonly string _configDir;
    private readonly WildCourtDbContext _db;
    private readonly FriendService _service;
    private const long UserId = 1;   // 偷者
    private const long FriendId = 2; // 被偷者

    public FriendServiceTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "wc_friend_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDir);
        File.WriteAllText(Path.Combine(_configDir, "Config_StealMechanics.json"),
            "{\"rows\":[{\"stealRatio\":0.3,\"dailyLimitPerCrop\":1,\"dailyLimitTotal\":20,\"matureOnly\":true,\"protectionDuration\":24}]}");
        File.WriteAllText(Path.Combine(_configDir, "Config_Crops.json"),
            "{\"rows\":[" +
            "{\"id\":\"C01\",\"name\":\"小麦\",\"growTime\":20,\"yield\":12,\"sellPrice\":5,\"processTo\":\"面粉\",\"processTime\":3}" +
            "]}");

        var options = new DbContextOptionsBuilder<WildCourtDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        _db = new WildCourtDbContext(options);
        _db.Users.AddRange(
            new UserEntity { UserId = UserId, Username = "thief", PasswordHash = "x", Nickname = "t", Level = 1, CreatedAt = DateTime.UtcNow },
            new UserEntity { UserId = FriendId, Username = "owner", PasswordHash = "x", Nickname = "o", Level = 1, CreatedAt = DateTime.UtcNow });
        _db.Friends.Add(new FriendEntity { UserId = UserId, FriendId = FriendId, CreatedAt = DateTime.UtcNow });
        _db.SaveChanges();

        _service = new FriendService(_db, new ConfigService(_configDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_configDir, recursive: true); } catch { /* 清理失败忽略 */ }
    }

    private void AddMaturePlot(int cellId = 0, int stolenCount = 0)
    {
        _db.FarmPlots.Add(new FarmPlotEntity
        {
            UserId = FriendId,
            CellId = cellId,
            CropId = "C01",
            PlantedAt = DateTime.UtcNow.AddMinutes(-30),
            ReadyAt = DateTime.UtcNow.AddMinutes(-1),
            State = "growing",
            StolenCount = stolenCount,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Steal_Success_AddsToThiefAndMarksPlot()
    {
        AddMaturePlot();

        var result = await _service.StealAsync(UserId, FriendId, 0);

        Assert.Equal(3, result.Count); // floor(12 * 0.3) = 3
        var inv = await _db.Inventories.SingleAsync(i => i.UserId == UserId && i.ItemId == "C01");
        Assert.Equal(3, inv.Count);
        var plot = await _db.FarmPlots.SingleAsync(p => p.UserId == FriendId && p.CellId == 0);
        Assert.Equal(3, plot.StolenCount);

        Assert.Single(_db.StealRecords);
        var tx = Assert.Single(_db.Transactions);
        Assert.Equal("steal", tx.Type);
        Assert.Equal(3, tx.Count);
    }

    [Fact]
    public async Task Steal_Self_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.StealAsync(UserId, UserId, 0));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("自己的作物", ex.Message);
    }

    [Fact]
    public async Task Steal_NotFriend_Throws()
    {
        _db.Friends.RemoveRange(_db.Friends);
        await _db.SaveChangesAsync();
        AddMaturePlot();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.StealAsync(UserId, FriendId, 0));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("还不是好友", ex.Message);
    }

    [Fact]
    public async Task Steal_NoPlot_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.StealAsync(UserId, FriendId, 3));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task Steal_NotMature_Throws()
    {
        _db.FarmPlots.Add(new FarmPlotEntity
        {
            UserId = FriendId,
            CellId = 0,
            CropId = "C01",
            PlantedAt = DateTime.UtcNow,
            ReadyAt = DateTime.UtcNow.AddMinutes(10),
            State = "growing",
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.StealAsync(UserId, FriendId, 0));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("尚未成熟", ex.Message);
    }

    [Fact]
    public async Task Steal_AllStolen_Throws()
    {
        AddMaturePlot(stolenCount: 12);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.StealAsync(UserId, FriendId, 0));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("已被偷光", ex.Message);
    }

    [Fact]
    public async Task Steal_SameCropSameDay_SecondRejected()
    {
        AddMaturePlot();
        await _service.StealAsync(UserId, FriendId, 0);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.StealAsync(UserId, FriendId, 0));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("今天已偷过", ex.Message);
    }

    [Fact]
    public async Task Steal_DailyTotalLimit_Rejected()
    {
        AddMaturePlot();
        // 预置 20 条今日偷菜记录（偷者视角，跨不同目标/格子，避免命中 per-crop 限制）
        for (int i = 0; i < 20; i++)
        {
            _db.StealRecords.Add(new StealRecordEntity
            {
                StealerUserId = UserId,
                TargetUserId = 100 + i,
                CropId = "C01",
                CellId = i,
                Count = 1,
                StolenAt = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.StealAsync(UserId, FriendId, 0));

        Assert.Equal(ApiCodes.BadRequest, ex.Code);
        Assert.Contains("已达上限", ex.Message);
    }

    [Fact(Skip = "喂食业务待实现")]
    public void Feed_RespectsThreeTimesPerDay()
    {
        throw new NotImplementedException();
    }
}
