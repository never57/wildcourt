using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using WildCourt.Mcp;
using WildCourt.Mcp.Tools;

// ===== 配置读取：环境变量优先，其次 appsettings.json =====
var url = Environment.GetEnvironmentVariable("WILDCOURT_ADMIN_URL")
    ?? Config.Get("Admin:BaseUrl")
    ?? "http://localhost:5000/api/v1/admin";

var username = Environment.GetEnvironmentVariable("WILDCOURT_ADMIN_USERNAME")
    ?? Config.Get("Admin:Username")
    ?? "admin";

var password = Environment.GetEnvironmentVariable("WILDCOURT_ADMIN_PASSWORD")
    ?? Config.Get("Admin:Password")
    ?? string.Empty;

if (string.IsNullOrEmpty(password))
{
    Console.Error.WriteLine("[WildCourt.Mcp] 未配置管理员密码，请设置环境变量 WILDCOURT_ADMIN_PASSWORD 或 appsettings.local.json 的 Admin:Password。");
}

// ===== 管理 API 客户端（登录/JWT/HTTP 封装）=====
var api = new AdminApiClient(url, username, password);
await DeployTools.InitializeAsync(api);
await DatabaseTools.InitializeAsync(api);

// ===== 构建 MCP Server（stdio 传输）=====
var builder = Host.CreateApplicationBuilder(args);

// 重要：所有日志输出到 stderr，避免污染 stdout 上的 MCP 协议通信
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<DeployTools>()
    .WithTools<DatabaseTools>();

await builder.Build().RunAsync();
