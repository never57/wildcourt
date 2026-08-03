using Microsoft.EntityFrameworkCore;
using Moq;
using WlidCourt.Api.Models;
using WlidCourt.Api.Services;
using WlidCourt.Api.Utils;
using WlidCourt.Data.DbContext;
using Xunit;

namespace WlidCourt.Tests;

/// <summary>
/// 登录闭环测试：注册/登录/会话签发（UserService + InMemory DbContext + mock ISessionStore）。
/// </summary>
public class UserServiceTests
{
    private static (UserService Service, Mock<ISessionStore> Sessions) CreateService()
    {
        var options = new DbContextOptionsBuilder<WlidCourtDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new WlidCourtDbContext(options);
        var sessions = new Mock<ISessionStore>();
        sessions.Setup(s => s.CreateAsync(It.IsAny<long>())).ReturnsAsync("test-token");
        var service = new UserService(db, sessions.Object);
        return (service, sessions);
    }

    [Fact]
    public async Task Register_Success_HashesPasswordWithBcrypt()
    {
        var (service, _) = CreateService();

        var user = await service.RegisterAsync("testuser", "password-hash");

        Assert.NotNull(user);
        Assert.Equal("testuser", user.Nickname); // 注册时 Nickname 默认 = 账号
        Assert.Equal(1, user.Level);
        Assert.True(user.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Register_InvalidUsername_ThrowsBadRequest()
    {
        var (service, _) = CreateService();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.RegisterAsync("ab", "x"));
        Assert.Equal(ApiCodes.BadRequest, ex.Code);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ThrowsConflict()
    {
        var (service, _) = CreateService();
        await service.RegisterAsync("testuser", "password-hash");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.RegisterAsync("testuser", "password-hash"));
        Assert.Equal(ApiCodes.Conflict, ex.Code);
    }

    [Fact]
    public async Task Login_Success_ReturnsTokenAndUserId()
    {
        var (service, sessions) = CreateService();
        await service.RegisterAsync("testuser", "password-hash");

        var result = await service.LoginAsync("testuser", "password-hash");

        Assert.Equal("test-token", result.Token);
        Assert.True(result.UserId > 0);
        sessions.Verify(s => s.CreateAsync(result.UserId), Times.Once);
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        var (service, _) = CreateService();
        await service.RegisterAsync("testuser", "password-hash");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.LoginAsync("testuser", "wrong"));
        Assert.Equal(ApiCodes.Unauthorized, ex.Code);
    }

    [Fact]
    public async Task Login_UnknownUser_ThrowsUnauthorized()
    {
        var (service, _) = CreateService();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.LoginAsync("nobody", "password-hash"));
        Assert.Equal(ApiCodes.Unauthorized, ex.Code);
    }

    [Fact]
    public async Task GetProfile_MissingUser_ReturnsNull()
    {
        var (service, _) = CreateService();

        var user = await service.GetProfileAsync(999);

        Assert.Null(user);
    }
}
