using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Middleware;

/// <summary>
/// 管理员操作审计：自动记录 /api/v1/admin 下的写操作（POST/PUT/DELETE）到 operation_logs。
/// 注册在 AdminAuthMiddleware 之后，此时 context.Items["AdminId"] 已注入。
/// 登录接口（无鉴权）也能记录：AdminId 为空，AdminName 从请求体 username 提取。
/// </summary>
public class AdminAuditMiddleware
{
    private static readonly string[] _sensitiveKeys =
        { "password", "oldpassword", "newpassword", "passwordhash", "token", "authorization" };

    private static readonly HashSet<string> _skipPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/admin/audit-logs",
        "/api/v1/admin/audit-actions",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<AdminAuditMiddleware> _logger;

    public AdminAuditMiddleware(RequestDelegate next, ILogger<AdminAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, WildCourtDbContext db)
    {
        var request = context.Request;
        var isAdminPath = request.Path.StartsWithSegments("/api/v1/admin");
        var isWrite = HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method) || HttpMethods.IsDelete(request.Method);

        if (!isAdminPath || !isWrite || _skipPaths.Contains(request.Path.Value ?? string.Empty))
        {
            await _next(context);
            return;
        }

        // 读取请求体摘要（不影响后续管道读取）
        string bodySummary = string.Empty;
        if (request.ContentLength > 0)
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            bodySummary = MaskSensitive(body);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            await TryRecordAsync(context, db, bodySummary, sw.ElapsedMilliseconds);
        }
    }

    private async Task TryRecordAsync(HttpContext context, WildCourtDbContext db, string details, long durationMs)
    {
        try
        {
            var request = context.Request;
            long? adminId = null;
            if (context.Items.TryGetValue("AdminId", out var value) && value is long id)
            {
                adminId = id;
            }

            var adminName = ResolveAdminName(context, db, adminId);
            var action = BuildAction(request);

            var log = new AdminOperationLogEntity
            {
                AdminId = adminId,
                AdminName = adminName,
                Action = action,
                Method = request.Method,
                Path = request.Path,
                Details = string.IsNullOrWhiteSpace(details) ? null : Truncate(details, 2000),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                StatusCode = context.Response.StatusCode,
                DurationMs = durationMs,
                CreatedAt = DateTime.UtcNow,   // 统一按 UTC 存储，前端 formatDate 会转成浏览器本地时区显示
            };

            db.AdminOperationLogs.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // 审计失败不影响业务
            _logger.LogWarning(ex, "写入操作审计日志失败");
        }
    }

    private string ResolveAdminName(HttpContext context, WildCourtDbContext db, long? adminId)
    {
        if (adminId is long id)
        {
            var name = db.Admins.AsNoTracking()
                .Where(a => a.AdminId == id)
                .Select(a => a.Username)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }

        // 登录等未鉴权请求：从请求体提取 username
        if (context.Request.ContentLength > 0 && context.Request.Body.CanSeek)
        {
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = reader.ReadToEndAsync().GetAwaiter().GetResult();
            context.Request.Body.Position = 0;
            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("username", out var u) && u.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        return u.GetString() ?? string.Empty;
                    }
                }
                catch
                {
                    // 忽略解析失败
                }
            }
        }

        return adminId?.ToString() ?? string.Empty;
    }

    /// <summary>由路由生成可读操作名：/api/v1/admin/player/update-currency -> player.update-currency</summary>
    private string BuildAction(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        var trimmed = path.TrimStart('/');
        // 去掉 /api/v1/admin/ 前缀
        const string prefix = "api/v1/admin/";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[prefix.Length..];
        }
        return trimmed.Replace('/', '.');
    }

    private static string MaskSensitive(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement.Clone();
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return body;
            }

            var masked = new System.Text.Json.Nodes.JsonObject();
            foreach (var prop in root.EnumerateObject())
            {
                var key = prop.Name;
                if (_sensitiveKeys.Contains(key.ToLowerInvariant()))
                {
                    masked[key] = "***";
                }
                else
                {
                    masked[key] = System.Text.Json.Nodes.JsonNode.Parse(prop.Value.GetRawText());
                }
            }
            return masked.ToJsonString();
        }
        catch
        {
            return body;
        }
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..max] + "...(truncated)";
    }
}
