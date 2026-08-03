using System.Collections.Concurrent;

namespace WlidCourt.Api.Utils;

/// <summary>
/// 会话存储：token → userId（文档 02 §2.3 user_session:{token}）。
/// 当前为进程内内存实现（开发期），待 RedisUtil 落实后替换为 Redis，
/// 仅需改本类内部实现，接口不变（迁移腾讯云时 Redis 就绪）。
/// </summary>
public class SessionStore
{
    private sealed record SessionEntry(long UserId, DateTime ExpiresAt);

    /// <summary>会话有效期（默认 7 天）。</summary>
    private static readonly TimeSpan SessionTtl = TimeSpan.FromDays(7);

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();

    /// <summary>为指定用户创建会话，返回 token。</summary>
    public string Create(long userId)
    {
        var token = Guid.NewGuid().ToString("N");
        _sessions[token] = new SessionEntry(userId, DateTime.UtcNow.Add(SessionTtl));
        return token;
    }

    /// <summary>校验 token，成功返回 true 并输出 userId。</summary>
    public bool TryGetUserId(string token, out long userId)
    {
        userId = 0;
        if (string.IsNullOrEmpty(token)) return false;

        if (_sessions.TryGetValue(token, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                userId = entry.UserId;
                return true;
            }
            _sessions.TryRemove(token, out _); // 过期清理
        }
        return false;
    }

    /// <summary>主动销毁会话（登出/封禁）。</summary>
    public void Remove(string token)
    {
        if (!string.IsNullOrEmpty(token)) _sessions.TryRemove(token, out _);
    }
}
