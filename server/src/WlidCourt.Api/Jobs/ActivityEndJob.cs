using Quartz;

namespace WlidCourt.Api.Jobs;

/// <summary>
/// 活动结束任务：关闭活动 + 补发未领奖励（文档 13）（文档 02 §2.4）。
/// </summary>
public class ActivityEndJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        throw new NotImplementedException();
    }
}
