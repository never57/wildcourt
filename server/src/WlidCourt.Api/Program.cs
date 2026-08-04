using Microsoft.EntityFrameworkCore;
using WlidCourt.Api.Hubs;
using WlidCourt.Api.Middleware;
using WlidCourt.Api.Services;
using WlidCourt.Api.Utils;
using WlidCourt.Data.DbContext;

var builder = WebApplication.CreateBuilder(args);

// 文档 15.2：appsettings 统一存放于 server/config/（通过 ASPNETCORE_ENVIRONMENT 切换）
// 开发期 ContentRoot = server/src/WlidCourt.Api，config 在其上级两级（../../config）；
// 生产可设置环境变量 WLIDCOURT_CONFIG_DIR 覆盖配置目录。
var configDir = Environment.GetEnvironmentVariable("WLIDCOURT_CONFIG_DIR")
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "config"));
builder.Configuration
    .AddJsonFile(Path.Combine(configDir, "appsettings.json"), optional: true, reloadOnChange: true)
    .AddJsonFile(Path.Combine(configDir, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(); // 云上部署：可用 ConnectionStrings__MySql / Redis__ConnectionString 等环境变量覆盖

// 数据层：MySQL（Pomelo）+ Redis（会话/匹配池/队列）
var connectionString = builder.Configuration.GetConnectionString("MySql")
    ?? "Server=localhost;Port=3306;Database=wlidcourt_dev;User=root;Password=;CharSet=utf8mb4;";
builder.Services.AddDbContext<WlidCourtDbContext>(opt =>
    opt.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton(new RedisUtil(redisConn));
builder.Services.AddSingleton<ISessionStore, SessionStore>();

// 数值配置（服务端与客户端同读 Config_*.json，文档 16 §16.3）
builder.Services.AddSingleton(new ConfigService(configDir));

// 3. 注册 Services（后续业务 Service 按需追加）
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<YardService>();

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
    var db = scope.ServiceProvider.GetRequiredService<WlidCourtDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "数据库迁移失败：服务以降级模式启动（MySQL 不可用）");
    }
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<AuthMiddleware>();
app.UseMiddleware<LogMiddleware>();

app.MapControllers();
app.MapHub<ChatHub>("/hub/chat");

app.Run();
