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
// ServerVersion.AutoDetect 会同步连 MySQL 探版本，连不上直接抛异常让 builder 失败。
// 生产环境 MySQL 偶发抖动/启动顺序问题时，降级为固定版本（与 docker-compose mysql:8.4 对齐），
// 避免容器因 AutoDetect 失败而崩溃 → 蓝绿部署误判 → 502。
var serverVersion = TryDetectMySqlVersion(connectionString);
builder.Services.AddDbContext<WildCourtDbContext>(opt =>
    opt.UseMySql(connectionString, serverVersion));

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
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<SignInService>();
builder.Services.AddScoped<LetterService>();

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

// 上传文件大小限制：头像等图片上传（最大 8MB，覆盖 multipart 上传）
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(opt =>
{
    opt.MultipartBodyLengthLimit = 8 * 1024 * 1024;
});
// 实时连接：原生 WebSocket（替代 SignalR，规避 IL2CPP/AOT 反射兼容性问题）
builder.Services.AddSingleton<RealtimeConnectionManager>();

var app = builder.Build();

// 4. 启动时确保数据库与表结构就绪（EF Core 迁移）
//    降级策略：迁移失败直接让进程退出（非 0），让 docker healthcheck 判定 unhealthy →
//    deploy.sh 回滚到旧 active 容器，避免新容器"看似启动但 schema 不对/业务 500"的半死状态。
//    （旧实现是 catch 后只记日志继续启动，导致 /health 仍 200 但业务全崩，蓝绿切换后才发现 → 已切流量无法回滚）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WildCourtDbContext>();
    var migrateLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        migrateLogger.LogCritical(ex, "数据库迁移失败：进程退出。deploy.sh 健康检查将判定 unhealthy 并回滚到旧容器");
        // 非零退出码：docker 标记容器 Exited(1)，healthcheck 立即 unhealthy，deploy.sh 回滚
        Environment.ExitCode = 1;
        // 给日志刷盘时间再退出
        await Task.Delay(500);
        return;
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

// WebSocket 需在中间件之前启用（原生 WebSocket 长连接 /ws/chat）
app.UseWebSockets();

// 静态文件托管：头像等上传资源（wwwroot/uploads/...）。
// 必须放在鉴权中间件之前，否则 /uploads/xxx 会被 AuthMiddleware 拦截返回 401。
var webRoot = builder.Configuration["Upload:RootDir"];
if (string.IsNullOrEmpty(webRoot))
{
    webRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
}
Directory.CreateDirectory(webRoot);
// 以 webRoot 为静态根（不带 RequestPath 前缀），使 URL /uploads/avatar/x.png 映射到 webRoot/uploads/avatar/x.png
var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
// 显式补全图片 MIME，避免 .png 被误判为 application/json
contentTypeProvider.Mappings[".png"] = "image/png";
contentTypeProvider.Mappings[".jpg"] = "image/jpeg";
contentTypeProvider.Mappings[".jpeg"] = "image/jpeg";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot),
    RequestPath = "",
    ContentTypeProvider = contentTypeProvider
});

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<RealtimeWebSocketMiddleware>();
app.UseMiddleware<AdminAuthMiddleware>();
app.UseMiddleware<AdminAuditMiddleware>();
app.UseMiddleware<AuthMiddleware>();
app.UseMiddleware<LogMiddleware>();

app.MapControllers();

// 容器健康检查端点（蓝绿部署/healthcheck 用）：
// 只确认进程可响应，不查 DB，避免迁移降级时误判并拖慢就绪判断
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
return;

// AutoDetect 失败时降级为固定 8.4.x 版本（与 docker-compose.yml 的 mysql:8.4 对齐）。
// 连不上 MySQL 通常是启动顺序/网络抖动，不应用致命异常阻断容器启动；
// 后续 Migrate 仍会真正连库，那时再失败由 Migrate 的 catch 决定是否退出。
static ServerVersion TryDetectMySqlVersion(string connectionString)
{
    try
    {
        return ServerVersion.AutoDetect(connectionString);
    }
    catch (Exception)
    {
        // 降级：与 docker-compose.yml 的 mysql:8.4 主版本对齐
        return new MySqlServerVersion(new Version(8, 4, 0));
    }
}
