namespace WlidCourt.Api.Utils;

/// <summary>
/// 会话存储接口：token → userId（文档 02 §2.3 user_session:{token}）。
/// 实现：<see cref="SessionStore"/>（Redis）。
/// </summary>
public interface ISessionStore
{
    /// <summary>为指定用户创建会话，返回 token。</summary>
    Task<string> CreateAsync(long userId);

    /// <summary>校验 token，成功返回 userId，失败/过期返回 null。</summary>
    Task<long?> TryGetUserIdAsync(string token);

    /// <summary>主动销毁会话（登出/封禁）。</summary>
    Task RemoveAsync(string token);
}
