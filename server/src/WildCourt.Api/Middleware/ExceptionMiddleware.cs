using WildCourt.Api.Models;

namespace WildCourt.Api.Middleware;

/// <summary>
/// 异常中间件：业务异常（BusinessException）→ { code, message }；系统异常 → { code:500 }（文档 02 §2.6）。
/// 统一 HTTP 200 + 业务信封，客户端按 envelope.Code 判断。
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            await WriteEnvelopeAsync(context, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未处理异常: {Path}", context.Request.Path);
            await WriteEnvelopeAsync(context, ApiCodes.ServerError, "服务器内部错误");
        }
    }

    private static async Task WriteEnvelopeAsync(HttpContext context, int code, string message)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = StatusCodes.Status200OK; // 业务信封统一 HTTP 200
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(code, message));
    }
}
