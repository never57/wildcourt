namespace WildCourt.Data.Entities;

/// <summary>
/// 管理员操作审计日志（operation_logs）。
/// 由 AdminAuditMiddleware 自动记录 /api/v1/admin 下的写操作，用于运营后台留痕审计。
/// </summary>
public class AdminOperationLogEntity
{
    public long LogId { get; set; }

    /// <summary>操作管理员 ID；登录接口未鉴权时为空。</summary>
    public long? AdminId { get; set; }

    /// <summary>操作管理员名（冗余，便于展示与筛选）。</summary>
    public string AdminName { get; set; } = string.Empty;

    /// <summary>操作类型，如 player.update_currency / admin.login / database.execute_sql。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>HTTP 方法：POST / PUT / DELETE。</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>请求路径。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>请求参数摘要（JSON 文本，敏感字段脱敏，截断至 2000 字符）。</summary>
    public string? Details { get; set; }

    /// <summary>客户端 IP。</summary>
    public string? IpAddress { get; set; }

    /// <summary>响应状态码。</summary>
    public int StatusCode { get; set; }

    /// <summary>请求耗时（毫秒）。</summary>
    public long DurationMs { get; set; }

    public DateTime CreatedAt { get; set; }
}
