namespace WlidCourt.Api.Middleware;

/// <summary>
/// 异常中间件：统一异常 → { code, message }；业务异常与系统异常分级（文档 02 §2.6）。
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch
        {
            // TODO: 统一异常处理：业务异常与系统异常分级返回 { code, message }。
            throw;
        }
    }
}
