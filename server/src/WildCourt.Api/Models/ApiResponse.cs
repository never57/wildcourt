namespace WildCourt.Api.Models;

/// <summary>
/// 统一响应信封 { code, data, message }（见文档 02 §2.2）。
/// </summary>
public class ApiResponse<T>
{
    public int Code { get; set; }

    public T? Data { get; set; }

    public string Message { get; set; } = string.Empty;

    public static ApiResponse<T> Ok(T data) => new() { Code = 0, Data = data };

    public static ApiResponse<T> Fail(int code, string message) => new() { Code = code, Message = message };
}
