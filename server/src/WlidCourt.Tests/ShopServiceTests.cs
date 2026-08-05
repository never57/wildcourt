using Microsoft.EntityFrameworkCore;
using WlidCourt.Api.Models;
using WlidCourt.Api.Services;
using WlidCourt.Data.DbContext;
using WlidCourt.Data.Entities;
using Xunit;

namespace WlidCourt.Tests;

/// <summary>
/// 商店测试（阶段 B，文档 09）：装饰货架、庭贝购买、余额不足、活动限定不可购、非法货币、记账。
/// </summary>
public class ShopServiceTests : IDisposable
{
    private readonly string _configDir;
    private readonly WlidCourtDbContext _db;
    private readonly ShopService _service;
    private const long UserId = 1;

    public ShopServiceTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "wc_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configDir);
        File.WriteAllText(Path.Combine(_configDir, "Config_Decorations.json"),
            "{\"rows\":[" +
            "{\"id\":\"D001\",\"name\":\"枫树\",\"category\":\"景观\",\"price\":100,\"sizeX\":1,\"sizeY\":1,\"rotatable\":true,\"acquire\":\"商店\",\"description\":\"\",\"maxCount\":2,\"effect\":\"leaves\"}," +
            "{\"id\":\"D013\",\"name\":\"红灯笼\",\"category\":\"节日限定\",\"price\":300,\"sizeX\":1,\"sizeY\":1,\"rotatable\":false,\"acquire\":\"春节活动\",\"description\":\"\",\"maxCount\":2,\"effect\":\"\"}" +
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

        _service = new ShopService(_db, new ConfigService(_configDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_configDir, recursive: true); } catch { /* 清理失败忽略 */ }
    }

    [Fact]
    public async Task GetShelves_ReturnsOnlyShopDecorations()
    {
        var shelves = await _service.GetShelvesAsync();

        var item = Assert.Single(shelves);
        Assert.Equal("D001", item.Id);
        Assert.Equal("枫树", item.ItemName);
        Assert.Equal(100, item.BuyPrice);
    }

    [Fact]
    public async Task Buy_Success_DeductsShell_AddsInventory_RecordsTransaction()
    {
        var result = await _service.BuyAsync(UserId, "D001", 2, "shell");

        Assert.Equal(800, result.Shell);
        Assert.Equal(1000 - 200, (await _db.Users.FindAsync(UserId))!.Shell);

        var inv = await _db.Inventories.SingleAsync(i => i.UserId == UserId && i.ItemId == "D001");
        Assert.Equal(2, inv.Count);

        var tx = Assert.Single(_db.Transactions);
        Assert.Equal("shop", tx.Type);
        Assert.Equal("D001", tx.ItemId);
        Assert.Equal(2, tx.Count);
        Assert.Equal(200, tx.Price);
    }

    [Fact]
    public async Task Buy_SecondPurchase_AccumulatesInventory()
    {
        await _service.BuyAsync(UserId, "D001", 1, "shell");
        await _service.BuyAsync(UserId, "D001", 1, "shell");

        var inv = await _db.Inventories.SingleAsync(i => i.UserId == UserId && i.ItemId == "D001");
        Assert.Equal(2, inv.Count);
        Assert.Equal(800, (await _db.Users.FindAsync(UserId))!.Shell);
    }

    [Fact]
    public async Task Buy_InsufficientShell_ThrowsAndKeepsState()
    {
        (await _db.Users.FindAsync(UserId))!.Shell = 50;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.BuyAsync(UserId, "D001", 1, "shell"));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);

        Assert.Equal(50, (await _db.Users.FindAsync(UserId))!.Shell);
        Assert.Empty(_db.Inventories);
        Assert.Empty(_db.Transactions);
    }

    [Fact]
    public async Task Buy_OverMaxCount_ThrowsAndKeepsState()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.BuyAsync(UserId, "D001", 3, "shell"));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);

        Assert.Equal(1000, (await _db.Users.FindAsync(UserId))!.Shell);
        Assert.Empty(_db.Inventories);
        Assert.Empty(_db.Transactions);
    }

    [Fact]
    public async Task Buy_ActivityOnlyDecoration_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.BuyAsync(UserId, "D013", 1, "shell"));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task Buy_InvalidCurrency_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.BuyAsync(UserId, "D001", 1, "coupon"));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task Buy_InvalidCount_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.BuyAsync(UserId, "D001", 0, "shell"));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }
}
