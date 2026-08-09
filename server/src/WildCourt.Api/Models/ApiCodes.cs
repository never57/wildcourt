namespace WildCourt.Api.Models;

/// <summary>
/// 统一业务码（与客户端 Net/Protocol/NetConst.cs 的 ApiCodes 对齐；0 成功，非 0 错误）。
/// 业务错误统一以 HTTP 200 + 信封 { code, message } 返回，客户端按 envelope.Code 判断。
/// </summary>
public static class ApiCodes
{
    public const int Success = 0;
    public const int BadRequest = 400;      // 参数/业务校验失败
    public const int Unauthorized = 401;    // 未登录/token 失效
    public const int Forbidden = 403;       // 无权限
    public const int NotFound = 404;        // 资源不存在
    public const int Conflict = 409;        // 状态冲突（如账号已存在）
    public const int ServerError = 500;     // 系统异常
}
