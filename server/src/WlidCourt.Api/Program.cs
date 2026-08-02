using WlidCourt.Api.Hubs;
using WlidCourt.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 文档 15.2：appsettings 统一存放于 server/config/（通过 ASPNETCORE_ENVIRONMENT 切换）
// 开发期 ContentRoot = server/src/WlidCourt.Api，config 在其上级两级（../../config）；
// 生产可设置环境变量 WLIDCOURT_CONFIG_DIR 覆盖配置目录。
var configDir = Environment.GetEnvironmentVariable("WLIDCOURT_CONFIG_DIR")
    ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "config"));
builder.Configuration
    .AddJsonFile(Path.Combine(configDir, "appsettings.json"), optional: true, reloadOnChange: true)
    .AddJsonFile(Path.Combine(configDir, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true);

// 2. 注册 DbContext(MySQL) + RedisUtil
//    builder.Services.AddDbContext<WlidCourtDbContext>(...);
//    builder.Services.AddSingleton<RedisUtil>(...);
// 3. 注册 Services / Jobs(Quartz) / Hubs(SignalR) / Middleware
//    builder.Services.AddScoped<UserService>();
//    builder.Services.AddScoped<PetService>();
//    builder.Services.AddScoped<MatchService>();
//    builder.Services.AddScoped<LetterService>();
//    builder.Services.AddScoped<FriendService>();
//    builder.Services.AddScoped<ShopService>();
//    builder.Services.AddScoped<YardService>();
//    builder.Services.AddScoped<SettlementService>();
//    builder.Services.AddScoped<MiniGameService>();
//    builder.Services.AddScoped<PlayerService>();
//    builder.Services.AddScoped<AuditService>();
//    builder.Services.AddQuartz(q => { /* 4 个 Job 的调度器配置，见文档 02 §2.4 */ });
//    builder.Services.AddSignalR();
// 4. 启动时加载 JSON 配置（Config_PlayerLevel.json 等，供 PlayerService/MatchService 使用，
//    见文档 16 §16.3：服务端同步部署 StreamingAssets/config 下同一份 JSON）
// 5. 校验 MySQL / Redis 连通性 → 失败快速退出
// 6. 启动调度器（DailySettlementJob / MatchPoolRefreshJob / ActivityEndJob / LetterAuditJob）

builder.Services.AddControllers();
builder.Services.AddSignalR();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<AuthMiddleware>();
app.UseMiddleware<LogMiddleware>();

app.MapControllers();
app.MapHub<ChatHub>("/hub/chat");

app.Run();
