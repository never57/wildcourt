using Xunit;

namespace WildCourt.Tests;

/// <summary>
/// 结算服务测试：十项结算、宕机补跑、个别玩家补算（文档 02 §2.10）。
/// </summary>
public class SettlementServiceTests
{
    [Fact(Skip = "待 SettlementService 业务实现后补全")]
    public void RunDailySettlement_ProcessesAllTenItems()
    {
        // TODO: 实现十项结算测试。
        throw new NotImplementedException();
    }

    [Fact(Skip = "待 SettlementService 业务实现后补全")]
    public void RecoverPlayer_RecomputesForSinglePlayer()
    {
        // TODO: 实现个别玩家补算测试。
        throw new NotImplementedException();
    }
}
