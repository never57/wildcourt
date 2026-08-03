namespace WlidCourt.Api.Services;

/// <summary>
/// 玩家业务：经验累计/等级计算（读 Config_PlayerLevel.json，等级 1~4 满级 1500 EXP）（文档 02 §2.3）。
/// </summary>
public class PlayerService
{
    /// <summary>经验累计</summary>
    public Task<int> AddExpAsync(long userId, int exp)
    {
        throw new NotImplementedException();
    }

    /// <summary>玩家等级</summary>
    public Task<int> GetLevelAsync(long userId)
    {
        throw new NotImplementedException();
    }
}
