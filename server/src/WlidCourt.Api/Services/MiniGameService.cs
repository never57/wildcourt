namespace WlidCourt.Api.Services;

/// <summary>
/// 小游戏业务：上限校验 + 二次验分 + 奖励（评级 S/A/B/C → 庭贝 50/35/20/10，每日上限 200）（文档 02 §2.3）。
/// </summary>
public class MiniGameService
{
    /// <summary>开始小游戏</summary>
    public Task<object> StartAsync(long userId, string gameId)
    {
        throw new NotImplementedException();
    }

    /// <summary>结束并验分（防作弊：服务端二次验分）</summary>
    public Task<object> FinishAsync(long userId, string gameId, int score)
    {
        throw new NotImplementedException();
    }
}
