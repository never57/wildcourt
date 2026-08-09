using System.Text;
using Microsoft.AspNetCore.Http;
using Moq;
using WildCourt.Api.Middleware;
using WildCourt.Api.Models;
using WildCourt.Api.Utils;
using Xunit;

namespace WildCourt.Tests;

/// <summary>
/// 鉴权中间件测试：放行路径、Bearer/access_token 提取、未认证 401 信封。
/// </summary>
public class AuthMiddlewareTests
{
    private static (AuthMiddleware Middleware, Mock<ISessionStore> Sessions) CreateMiddleware()
    {
        var sessions = new Mock<ISessionStore>();
        sessions.Setup(s => s.TryGetUserIdAsync("valid-token")).ReturnsAsync((long?)42L);
        sessions.Setup(s => s.TryGetUserIdAsync(It.Is<string>(t => t != "valid-token")))
            .ReturnsAsync((long?)null);
        var middleware = new AuthMiddleware(_ => Task.CompletedTask, sessions.Object);
        return (middleware, sessions);
    }

    private static DefaultHttpContext CreateContext(string path, string? authorization = null, string? accessToken = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream(); // DefaultHttpContext 默认 NullStream，需替换为可读流
        if (!string.IsNullOrEmpty(authorization))
        {
            context.Request.Headers.Authorization = authorization;
        }
        if (!string.IsNullOrEmpty(accessToken))
        {
            context.Request.QueryString = new QueryString($"?access_token={accessToken}");
        }
        return context;
    }

    [Fact]
    public async Task AllowAnonymous_LoginPath_PassesThrough()
    {
        var (middleware, _) = CreateMiddleware();

        var context = CreateContext("/api/v1/user/login");
        await middleware.InvokeAsync(context);

        Assert.Null(context.Items["UserId"]);
    }

    [Fact]
    public async Task AllowAnonymous_RegisterPath_PassesThrough()
    {
        var (middleware, _) = CreateMiddleware();

        var context = CreateContext("/api/v1/user/register");
        await middleware.InvokeAsync(context);

        Assert.Null(context.Items["UserId"]);
    }

    [Fact]
    public async Task AllowAnonymous_HubPath_PassesThrough()
    {
        var (middleware, _) = CreateMiddleware();

        var context = CreateContext("/hub/chat");
        await middleware.InvokeAsync(context);

        Assert.Null(context.Items["UserId"]);
    }

    [Fact]
    public async Task ValidBearerToken_InjectsUserId()
    {
        var (middleware, _) = CreateMiddleware();

        var context = CreateContext("/api/v1/user/profile", authorization: "Bearer valid-token");
        await middleware.InvokeAsync(context);

        Assert.Equal(42L, context.Items["UserId"]);
    }

    [Fact]
    public async Task ValidAccessTokenQuery_InjectsUserId()
    {
        var (middleware, _) = CreateMiddleware();

        var context = CreateContext("/hub/chat?access_token=valid-token", accessToken: "valid-token");
        // Hub 路径放行不校验；此处改走受保护路径验证 access_token 提取：
        context.Request.Path = "/api/v1/user/profile";
        await middleware.InvokeAsync(context);

        Assert.Equal(42L, context.Items["UserId"]);
    }

    [Fact]
    public async Task NoToken_Returns401Envelope()
    {
        var (middleware, _) = CreateMiddleware();

        var context = CreateContext("/api/v1/user/profile");
        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode); // 业务信封统一 HTTP 200
        var body = System.Text.Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.Contains("\"code\":401", body);
    }

    [Fact]
    public async Task NoToken_InventoryPath_Returns401Envelope()
    {
        var (middleware, _) = CreateMiddleware();

        var context = CreateContext("/api/v1/user/inventory");
        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var body = System.Text.Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.Contains("\"code\":401", body);
    }

    [Fact]
    public async Task InvalidToken_Returns401Envelope()
    {
        var (middleware, _) = CreateMiddleware();

        var context = CreateContext("/api/v1/user/profile", authorization: "Bearer bad-token");
        await middleware.InvokeAsync(context);

        var body = System.Text.Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.Contains("\"code\":401", body);
    }
}
