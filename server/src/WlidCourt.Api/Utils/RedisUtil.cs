using StackExchange.Redis;

namespace WlidCourt.Api.Utils;

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
}
