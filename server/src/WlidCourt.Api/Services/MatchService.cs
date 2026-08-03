namespace WlidCourt.Api.Services;

/// <summary>
/// 匹配业务：匹配池维护 + 目标选取（权重 60/20/10/10，Redis ZRevRange + 过滤 + NPC 补位）（文档 02 §2.3）。
/// </summary>
public class MatchService
{
    /// <summary>选取匹配目标（对应 MatchMaker.FindTargets）</summary>
    public Task<List<long>> FindTargetsAsync(int provinceId, int? cityId, int count)
    {
        throw new NotImplementedException();
    }

    /// <summary>刷新匹配池</summary>
    public Task RefreshPoolAsync(int provinceId)
    {
        throw new NotImplementedException();
    }
}
