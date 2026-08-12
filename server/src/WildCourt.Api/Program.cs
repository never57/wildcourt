using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Hubs;
using WildCourt.Api.Middleware;
using WildCourt.Api.Models;
using WildCourt.Api.Services;
using WildCourt.Api.Utils;
using WildCourt.Data.DbContext;

var builder = WebApplication.CreateBuilder(args);

// 文档 15.2：appsettings 统一存放于 server/config/（通过 ASPNETCORE_ENVIRONMENT 切换）
// 开发期 ContentRoot = server/src/WildCourt.Api，config 在其上级两级（../../config）；
// 生产可设置环境变量 WILDCOURT_CONFIG_DIR 覆盖配置目录。
var configDir = Environment.GetEnvironmentVariable("WILDCOURT_CONFIG_DIR")
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "config"));
builder.Configuration
    .AddJsonFile(Path.Combine(configDir, "appsettings.json"), optional: true, reloadOnChange: true)
    .AddJsonFile(Path.Combine(configDir, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(); // 云上部署：可用 ConnectionStrings__MySql / Redis__ConnectionString 等环境变量覆盖

// 数据层：MySQL（Pomelo）+ Redis（会话/匹配池/队列）
var connectionString = builder.Configuration.GetConnectionString("MySql")
    ?? "Server=localhost;Port=3306;Database=wildcourt_dev;User=root;Password=;CharSet=utf8mb4;";
builder.Services.AddDbContext<WildCourtDbContext>(opt =>
    opt.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton(new RedisUtil(redisConn));
builder.Services.AddSingleton<ISessionStore, SessionStore>();

// 数值配置（服务端与客户端同读 Config_*.json，文档 16 §16.3）
builder.Services.AddSingleton(new ConfigService(configDir));

// 3. 注册 Services（后续业务 Service 按需追加）
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<YardService>();
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<ShopService>();
builder.Services.AddScoped<CropService>();
builder.Services.AddScoped<FriendService>();
builder.Services.AddScoped<SignInService>();

// 运营后台：令牌服务 + 业务服务 + 监控/部署
var adminSecret = builder.Configuration["Admin:JwtSecret"] ?? "wildcourt-admin-secret-change-me";
var adminTtlHours = double.TryParse(builder.Configuration["Admin:JwtExpireHours"], out var h) ? h : 24;
builder.Services.AddSingleton(new AdminTokenService(adminSecret, TimeSpan.FromHours(adminTtlHours)));
builder.Services.AddSingleton<CaptchaService>();
builder.Services.AddSingleton<TotpService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<AdminAuditService>();
builder.Services.AddSingleton<DockerService>();
builder.Services.AddSingleton<DeployService>();
builder.Services.AddScoped<DatabaseAdminService>();

// 统一 JSON 信封：camelCase 输出 + 大小写不敏感反序列化（对齐客户端小写字段 DTO）
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opt.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddSignalR();

var app = builder.Build();

// 4. 启动时确保数据库与表结构就绪（EF Core 迁移）
//    降级策略：MySQL 不可达仅记录错误并继续启动（本地无库/库未起时服务可起，
//    业务接口此时返回 500；生产建议保证 MySQL 就绪再启动，见 docs/deployment-issues.md）。
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WildCourtDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "数据库迁移失败：服务以降级模式启动（MySQL 不可用）");
    }

    // 5. 初始化默认管理员（仅当 admins 表为空，密码取配置，生产务必修改）
    try
    {
        var adminService = scope.ServiceProvider.GetRequiredService<AdminService>();
        var username = builder.Configuration["Admin:DefaultUsername"] ?? "admin";
        var rawPassword = builder.Configuration["Admin:DefaultPassword"] ?? "admin888";
        var passwordSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPassword))).ToLowerInvariant();
        var displayName = builder.Configuration["Admin:DefaultDisplayName"] ?? "运营管理员";
        await adminService.CreateAdminAsync(username, passwordSha256, displayName, AdminPermissions.RoleSuperAdmin);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("默认管理员初始化跳过：{Message}", ex.Message);
    }
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<AdminAuthMiddleware>();
app.UseMiddleware<AdminAuditMiddleware>();
app.UseMiddleware<AuthMiddleware>();
app.UseMiddleware<LogMiddleware>();

app.MapControllers();

// 容器健康检查端点（蓝绿部署/healthcheck 用）：
// 只确认进程可响应，不查 DB，避免迁移降级时误判并拖慢就绪判断
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapHub<ChatHub>("/hub/chat");

app.Run();
