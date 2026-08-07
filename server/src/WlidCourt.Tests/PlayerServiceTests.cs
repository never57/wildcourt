using Microsoft.EntityFrameworkCore;
using WlidCourt.Api.Models;
using WlidCourt.Api.Services;
using WlidCourt.Data.DbContext;
using WlidCourt.Data.Entities;
using Xunit;

namespace WlidCourt.Tests;

/// <summary>
/// 玩家经验/等级测试（文档 03 §3.5）：经验累计、等级阈值（0/200/600/1500）、非法输入与缺失用户校验。
/// </summary>
public class PlayerServiceTests : IDisposable
{
    private readonly string _configDir;
    private readonly WlidCourtDbContext _db;
    private readonly PlayerService _service;
    private const long UserId = 1;

    public PlayerServiceTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "wc_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDir);
        File.WriteAllText(Path.Combine(_configDir, "Config_PlayerLevel.json"),
            "{\"rows\":[" +
            "{\"level\":1,\"totalExp\":0,\"yardProgress\":1,\"note\":\"\"}," +
            "{\"level\":2,\"totalExp\":200,\"yardProgress\":2,\"note\":\"\"}," +
            "{\"level\":3,\"totalExp\":600,\"yardProgress\":3,\"note\":\"\"}," +
            "{\"level\":4,\"totalExp\":1500,\"yardProgress\":4,\"note\":\"\"}" +
            "]}");

        var options = new DbContextOptionsBuilder<WlidCourtDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        _db = new WlidCourtDbContext(options);
        _db.Users.Add(new UserEntity
        {
            UserId = UserId,
            Username = "tester",
            PasswordHash = "x",
            Nickname = "t",
            Level = 1,
            Shell = 1000,
            CreatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();

        _service = new PlayerService(_db, new ConfigService(_configDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_configDir, recursive: true); } catch { /* 清理失败忽略 */ }
    }

    [Fact]
    public async Task AddExp_AccumulatesAndRaisesLevel()
    {
        Assert.Equal(1, await _service.AddExpAsync(UserId, 100));   // 累计 100
        Assert.Equal(2, await _service.AddExpAsync(UserId, 100));   // 累计 200 → 2 级
        Assert.Equal(3, await _service.AddExpAsync(UserId, 400));   // 累计 600 → 3 级
        Assert.Equal(4, await _service.AddExpAsync(UserId, 900));   // 累计 1500 → 4 级
        Assert.Equal(4, await _service.AddExpAsync(UserId, 100));   // 超过满级仍 4 级
    }

    [Fact]
    public async Task GetLevel_ReturnsStoredLevel()
    {
        await _service.AddExpAsync(UserId, 600);

        Assert.Equal(3, await _service.GetLevelAsync(UserId));
    }

    [Fact]
    public async Task AddExp_NonPositive_ThrowsBadRequest()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.AddExpAsync(UserId, 0));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task AddExp_MissingUser_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.AddExpAsync(999, 50));
        Assert.Equal(ApiCodes.NotFound, ex.Code);
    }
}
