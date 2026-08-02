using Xunit;

namespace WlidCourt.Tests;

/// <summary>
/// 结算服务测试：十项结算、宕机补跑、个别玩家补算（文档 02 §2.10）。
/// </summary>
public class SettlementServiceTests
{
    [Fact]
    public void RunDailySettlement_ProcessesAllTenItems()
    {
        // TODO: 实现十项结算测试。
        throw new NotImplementedException();
    }

    [Fact]
    public void RecoverPlayer_RecomputesForSinglePlayer()
    {
        // TODO: 实现个别玩家补算测试。
        throw new NotImplementedException();
    }
}
