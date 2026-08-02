namespace WlidCourt.Api.Utils;

/// <summary>
/// Redis 封装：String / Sorted Set / List 操作，覆盖 8 类 Key（文档 02 §2.7、文档 15.3）。
/// </summary>
public class RedisUtil
{
    public Task<string?> GetStringAsync(string key)
    {
        throw new NotImplementedException();
    }

    public Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        throw new NotImplementedException();
    }

    public Task<double> AddToSortedSetAsync(string key, long member, double score)
    {
        throw new NotImplementedException();
    }

    public Task<List<long>> RangeByScoreAsync(string key, double min, double max, int count)
    {
        throw new NotImplementedException();
    }

    public Task PushToListAsync(string key, string value)
    {
        throw new NotImplementedException();
    }
}
