using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WlidCourt.Api.Models;
using WlidCourt.Api.Services;
using WlidCourt.Data.DbContext;
using WlidCourt.Data.Entities;
using Xunit;

namespace WlidCourt.Tests;

/// <summary>
/// 庭院业务测试（文档 03）：默认庭院、布局保存、容量/重叠/装饰校验、槽位、互访。
/// 使用 InMemory DbContext + 临时目录 JSON 配置（ConfigService 读文件）。
/// </summary>
public class YardServiceTests : IDisposable
{
    private readonly string _configDir;
    private readonly WlidCourtDbContext _db;
    private readonly YardService _service;
    private const long UserId = 1;

    public YardServiceTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "wc_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDir);
        // 容量：decorationLimit=3，便于测试超限
        File.WriteAllText(Path.Combine(_configDir, "Config_YardUnlock.json"),
            "{\"rows\":[{\"level\":1,\"farmlandCount\":6,\"processorSlots\":2,\"foodBowlCapacity\":10,\"decorationLimit\":3,\"showcaseSlots\":3,\"mailboxCapacity\":20,\"unlockCond\":\"Lv1\"}]}");
        File.WriteAllText(Path.Combine(_configDir, "Config_Decorations.json"),
            "{\"rows\":[" +
            "{\"id\":\"D001\",\"name\":\"枫树\",\"category\":\"景观\",\"price\":100,\"sizeX\":1,\"sizeY\":1,\"rotatable\":true,\"acquire\":\"商店\",\"description\":\"\",\"maxCount\":2,\"effect\":\"leaves\"}," +
            "{\"id\":\"D002\",\"name\":\"长凳\",\"category\":\"家具\",\"price\":120,\"sizeX\":2,\"sizeY\":1,\"rotatable\":true,\"acquire\":\"商店\",\"description\":\"\",\"maxCount\":2,\"effect\":\"\"}," +
            "{\"id\":\"D003\",\"name\":\"桂花树\",\"category\":\"景观\",\"price\":200,\"sizeX\":2,\"sizeY\":2,\"rotatable\":false,\"acquire\":\"商店\",\"description\":\"\",\"maxCount\":2,\"effect\":\"leaves\"}" +
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
            CreatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
        // 归属校验前置：拥有 D001 / D002（D003 配置存在但未拥有，用于未拥有用例）
        _db.Inventories.AddRange(
            new InventoryEntity { UserId = UserId, ItemId = "D001", Count = 1 },
            new InventoryEntity { UserId = UserId, ItemId = "D002", Count = 1 });
        _db.SaveChanges();

        _service = new YardService(_db, new ConfigService(_configDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_configDir, recursive: true); } catch { /* 清理失败忽略 */ }
    }

    private static YardItemDto Item(string id, int x, int y, int rotation = 0) =>
        new() { Id = id, X = x, Y = y, Rotation = rotation };

    [Fact]
    public async Task GetAsync_NoYard_CreatesDefaultThreeSlots()
    {
        var yard = await _service.GetAsync(UserId);

        Assert.Equal(UserId, yard.UserId);
        Assert.Equal(3, yard.Slots.Count);
        Assert.All(yard.Slots, s => Assert.Empty(s.Items));
        Assert.Equal(1, yard.CurrentSlot);
        Assert.Equal(3, yard.Cap.DecorationLimit);
    }

    [Fact]
    public async Task SaveLayout_ValidItems_SavesToSlot()
    {
        var items = new List<YardItemDto> { Item("D001", 0, 0), Item("D002", 3, 3) };

        var yard = await _service.SaveLayoutAsync(UserId, 2, items);

        var slot = yard.Slots.Single(s => s.SlotIndex == 2);
        Assert.Equal(2, slot.Items.Count);
        Assert.Equal(2, yard.CurrentSlot);
    }

    [Fact]
    public async Task SaveLayout_OverDecorationLimit_Throws()
    {
        var items = new List<YardItemDto>
        {
            Item("D001", 0, 0), Item("D001", 2, 0), Item("D001", 4, 0), Item("D001", 6, 0), // 4 个 > 上限 3
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.SaveLayoutAsync(UserId, 1, items));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task SaveLayout_OverlappingItems_Throws()
    {
        var items = new List<YardItemDto> { Item("D001", 0, 0), Item("D001", 0, 0) }; // 同格重叠

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.SaveLayoutAsync(UserId, 1, items));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task SaveLayout_InvalidDecorationId_Throws()
    {
        var items = new List<YardItemDto> { Item("D999", 0, 0) };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.SaveLayoutAsync(UserId, 1, items));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task SaveLayout_OutOfBounds_Throws()
    {
        // 1×1 装饰放在 x=20：超出围墙 0~19（文档 03 §3.8 边界限制）
        var items = new List<YardItemDto> { Item("D001", 20, 0) };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.SaveLayoutAsync(UserId, 1, items));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task SaveLayout_MultiCellDecorationOutOfBounds_Throws()
    {
        // D002 为 2×1，x=19 时右缘越界（19+2 > 20）
        var items = new List<YardItemDto> { Item("D002", 19, 0) };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.SaveLayoutAsync(UserId, 1, items));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task SaveLayout_NotOwnedDecoration_Throws()
    {
        // D003 配置存在但玩家未拥有（inventories 无记录）
        var items = new List<YardItemDto> { Item("D003", 0, 0) };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.SaveLayoutAsync(UserId, 1, items));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task SaveLayout_ExceedsMaxCount_Throws()
    {
        // maxCount=2：3 个 D001 先过总数上限（3 ≤ 3），再被同 ID 上限拦下
        var items = new List<YardItemDto>
        {
            Item("D001", 0, 0), Item("D001", 2, 0), Item("D001", 4, 0),
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.SaveLayoutAsync(UserId, 1, items));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task SaveLayout_Mirror_RoundTrips()
    {
        var items = new List<YardItemDto> { Item("D001", 0, 0) };
        items[0].Mirror = true;

        await _service.SaveLayoutAsync(UserId, 1, items);
        var yard = await _service.GetAsync(UserId);

        var slot = yard.Slots.Single(s => s.SlotIndex == 1);
        Assert.True(slot.Items.Single().Mirror);
    }

    [Fact]
    public async Task SaveLayout_InvalidSlot_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.SaveLayoutAsync(UserId, 4, new List<YardItemDto>()));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task SaveLayout_SwitchingSlots_KeepsOtherSlots()
    {
        await _service.SaveLayoutAsync(UserId, 1, new List<YardItemDto> { Item("D001", 0, 0) });

        var yard = await _service.SaveLayoutAsync(UserId, 2, new List<YardItemDto> { Item("D002", 1, 1) });

        var slot1 = yard.Slots.Single(s => s.SlotIndex == 1);
        Assert.Single(slot1.Items); // 槽 1 保留
    }

    [Fact]
    public async Task Visit_ReturnsTargetYard()
    {
        var yard = await _service.VisitAsync(UserId);

        Assert.Equal(UserId, yard.UserId);
    }
}
