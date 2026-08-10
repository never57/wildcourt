using StackExchange.Redis;

namespace WildCourt.Api.Utils;

/// <summary>
/// Redis 封装：String / Sorted Set / List 操作，覆盖 8 类 Key（文档 02 §2.7、文档 15.3）。
/// 基于 StackExchange.Redis；连接串来自 appsettings["Redis:ConnectionString"]（如 localhost:6379）。
/// </summary>
public class RedisUtil
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public RedisUtil(string connectionString)
    {
        // abortConnect=false：Redis 未就绪时不抛异常（后台重试），与启动降级策略一致
        // （Program.cs 迁移降级 + Redis 可后起），也便于 dotnet ef 设计时构建 host。
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        _redis = ConnectionMultiplexer.Connect(options);
        _db = _redis.GetDatabase();
    }

    // ---- String ----

    public async Task<string?> GetStringAsync(string key)
    {
        var value = await _db.StringGetAsync(key);
        return value.IsNull ? null : value.ToString();
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        await _db.StringSetAsync(key, value, expiry);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        return await _db.KeyDeleteAsync(key);
    }

    // ---- Sorted Set ----

    public async Task<double> AddToSortedSetAsync(string key, long member, double score)
    {
        return await _db.SortedSetIncrementAsync(key, member, score);
    }

    public async Task<List<long>> RangeByScoreAsync(string key, double min, double max, int count)
    {
        var values = await _db.SortedSetRangeByScoreAsync(key, min, max, Exclude.None, Order.Descending, 0, count);
        var result = new List<long>(values.Length);
        foreach (var v in values)
        {
            if (long.TryParse(v.ToString(), out var id)) result.Add(id);
        }
        return result;
    }

    // ---- List ----

    public async Task PushToListAsync(string key, string value)
    {
        await _db.ListRightPushAsync(key, value);
    }

    // ---- Key 统计 / 批量删除（运营后台用） ----

    /// <summary>
    /// 统计匹配 pattern 的 key 数量（如 "user_session:*"）。用 SCAN 遍历，勿在生产大库使用频繁。
    /// </summary>
    public async Task<long> CountKeysAsync(string pattern)
    {
        var server = GetServer();
        if (server is null) return 0;
        long count = 0;
        await foreach (var key in server.KeysAsync(_db.Database, pattern, pageSize: 500))
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// 删除匹配 pattern 的所有 key，返回删除数量（用于封禁时清理该用户的全部会话）。
    /// </summary>
    public async Task<long> DeleteByPatternAsync(string pattern)
    {
        var server = GetServer();
        if (server is null) return 0;
        long deleted = 0;
        var batch = _db.CreateBatch();
        var tasks = new List<Task>();
        await foreach (var key in server.KeysAsync(_db.Database, pattern, pageSize: 500))
        {
            tasks.Add(batch.KeyDeleteAsync(key));
            if (tasks.Count >= 200)
            {
                batch.Execute();
                deleted += tasks.Count;
                tasks.Clear();
                batch = _db.CreateBatch();
            }
        }
        if (tasks.Count > 0)
        {
            batch.Execute();
            deleted += tasks.Count;
        }
        return deleted;
    }

    /// <summary>Redis 在线会话 key 前缀（与 SessionStore 保持一致）。</summary>
    public const string SessionKeyPrefix = "user_session:";

    /// <summary>
    /// Redis 实时在线标记 key 前缀：user_online:{userId}，短 TTL 滑动续期。
    /// 仅用于管理后台"实时在线"统计（区别于 7 天长效的 user_session）。
    /// </summary>
    public const string OnlineKeyPrefix = "user_online:";

    /// <summary>刷新实时在线标记（鉴权通过的每次请求调用），TTL 内无请求视为离线。</summary>
    public async Task TouchOnlineAsync(long userId, TimeSpan ttl)
    {
        await _db.StringSetAsync(OnlineKeyPrefix + userId, DateTime.UtcNow.ToString("O"), ttl);
    }

    /// <summary>
    /// 删除指定用户的全部在线会话（封禁踢下线用）。
    /// key = user_session:{token}，value = userId，需 SCAN + GET 过滤后删除。
    /// </summary>
    public async Task<long> DeleteSessionsOfUserAsync(long userId)
    {
        var server = GetServer();
        if (server is null) return 0;
        var userStr = userId.ToString();
        long deleted = 0;
        var batch = _db.CreateBatch();
        var tasks = new List<Task>();
        var pending = new List<RedisKey>();
        await foreach (var key in server.KeysAsync(_db.Database, SessionKeyPrefix + "*", pageSize: 500))
        {
            pending.Add(key);
            if (pending.Count >= 200)
            {
                var values = await _db.StringGetAsync(pending.ToArray());
                for (var i = 0; i < values.Length; i++)
                {
                    if (values[i] == userStr) tasks.Add(batch.KeyDeleteAsync(pending[i]));
                }
                if (tasks.Count > 0)
                {
                    batch.Execute();
                    deleted += tasks.Count;
                    tasks.Clear();
                    batch = _db.CreateBatch();
                }
                pending.Clear();
            }
        }
        if (pending.Count > 0)
        {
            var values = await _db.StringGetAsync(pending.ToArray());
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] == userStr) tasks.Add(batch.KeyDeleteAsync(pending[i]));
            }
            if (tasks.Count > 0)
            {
                batch.Execute();
                deleted += tasks.Count;
            }
        }
        return deleted;
    }

    private IServer? GetServer()
    {
        var endpoints = _redis.GetEndPoints();
        if (endpoints.Length == 0) return null;
        var server = _redis.GetServer(endpoints[0]);
        return server.IsConnected ? server : null;
    }
}
