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
    .AddJsonFile(Path.Combine(configDir, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true);

// 2. 注册 DbContext(MySQL) + 会话存储
var connectionString = builder.Configuration.GetConnectionString("MySql")
    ?? "Server=localhost;Port=3306;Database=wlidcourt_dev;User=root;Password=;CharSet=utf8mb4;";
builder.Services.AddDbContext<WlidCourtDbContext>(opt =>
    opt.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddSingleton<SessionStore>(); // Redis 就绪前为内存会话（见 SessionStore 注释）

// 3. 注册 Services（后续业务 Service 按需追加）
builder.Services.AddScoped<UserService>();

// 统一 JSON 信封：camelCase 输出 + 大小写不敏感反序列化（对齐客户端小写字段 DTO）
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opt.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddSignalR();

var app = builder.Build();

// 4. 启动时确保数据库与表结构就绪（EF Core 迁移；开发期自动建库建表）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WlidCourtDbContext>();
    db.Database.Migrate();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<AuthMiddleware>();
app.UseMiddleware<LogMiddleware>();

app.MapControllers();
app.MapHub<ChatHub>("/hub/chat");

app.Run();
