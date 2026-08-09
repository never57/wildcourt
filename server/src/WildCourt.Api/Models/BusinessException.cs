namespace WildCourt.Api.Models;

/// <summary>
/// 业务异常：由 ExceptionMiddleware 捕获并转换为 { code, message } 信封（HTTP 200）。
/// </summary>
public class BusinessException : Exception
{
    /// <summary>业务码（见 <see cref="ApiCodes"/>）。</summary>
    public int Code { get; }

    public BusinessException(int code, string message) : base(message)
    {
        Code = code;
    }
}
