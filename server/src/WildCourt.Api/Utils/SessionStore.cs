namespace WildCourt.Api.Utils;

/// <summary>
/// 会话存储（Redis 实现）：key = user_session:{token}，value = userId，TTL 7 天。
/// 多实例共享、重启不掉线；token 无滑动续期（固定 7 天，重复登录产生多个并存 token，可接受）。
/// </summary>
public class SessionStore : ISessionStore
{
    private const string KeyPrefix = "user_session:";

    /// <summary>会话有效期（默认 7 天）。</summary>
    private static readonly TimeSpan SessionTtl = TimeSpan.FromDays(7);

    private readonly RedisUtil _redis;

    public SessionStore(RedisUtil redis)
    {
        _redis = redis;
    }

    public async Task<string> CreateAsync(long userId)
    {
        var token = Guid.NewGuid().ToString("N");
        await _redis.SetStringAsync(KeyPrefix + token, userId.ToString(), SessionTtl);
        return token;
    }

    public async Task<long?> TryGetUserIdAsync(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var value = await _redis.GetStringAsync(KeyPrefix + token);
        return long.TryParse(value, out var userId) ? userId : null;
    }

    public async Task RemoveAsync(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            await _redis.DeleteAsync(KeyPrefix + token);
        }
    }
}
