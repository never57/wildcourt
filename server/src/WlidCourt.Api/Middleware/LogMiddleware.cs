namespace WlidCourt.Api.Middleware;

/// <summary>
/// 日志中间件：关键操作写 operation_logs（user_id/action/details/created_at）（文档 02 §2.6）。
/// </summary>
public class LogMiddleware
{
    private readonly RequestDelegate _next;

    public LogMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // TODO: 记录关键操作到 operation_logs。
        await _next(context);
    }
}
