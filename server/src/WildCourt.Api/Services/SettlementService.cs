namespace WildCourt.Api.Services;

/// <summary>
/// 每日结算业务：十项结算（文档 04 §4.3）、宕机补跑、个别玩家补算（文档 02 §2.3）。
/// </summary>
public class SettlementService
{
    /// <summary>每日结算（十项）</summary>
    public Task RunDailySettlementAsync(DateTime settleDate)
    {
        throw new NotImplementedException();
    }

    /// <summary>宕机/个别失败补算</summary>
    public Task RecoverPlayerAsync(long userId, DateTime settleDate)
    {
        throw new NotImplementedException();
    }
}
