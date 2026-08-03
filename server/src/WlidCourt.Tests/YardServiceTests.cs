using Xunit;

namespace WlidCourt.Tests;

/// <summary>
/// 庭院服务测试：布局保存、等级容量校验（文档 02 §2.10）。
/// </summary>
public class YardServiceTests
{
    [Fact(Skip = "待 YardService 业务实现后补全")]
    public void SaveLayout_RejectsOverCapacity()
    {
        // TODO: 实现等级容量校验测试。
        throw new NotImplementedException();
    }

    [Fact(Skip = "待 YardService 业务实现后补全")]
    public void SaveLayout_SavesValidLayout()
    {
        // TODO: 实现合法布局保存测试。
        throw new NotImplementedException();
    }
}
