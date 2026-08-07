using Xunit;

namespace WlidCourt.Tests;

/// <summary>
/// 匹配服务测试：权重排序、城市/省池回退、NPC 补位、不可用过滤（文档 02 §2.10）。
/// </summary>
public class MatchServiceTests
{
    [Fact(Skip = "待 MatchService 业务实现后补全")]
    public void FindTargets_ReturnsWeightedTargets()
    {
        // TODO: 实现权重排序测试。
        throw new NotImplementedException();
    }

    [Fact(Skip = "待 MatchService 业务实现后补全")]
    public void FindTargets_FallsBackToProvincePool()
    {
        // TODO: 实现城市池不足时回退省池测试。
        throw new NotImplementedException();
    }
}
