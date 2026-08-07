using Quartz;

namespace WlidCourt.Api.Jobs;

/// <summary>
/// 每日结算任务：每日 04:00（UTC+8）执行 SettlementService.RunDailySettlementAsync（十项，见文档 04 §4.3）；宕机恢复后补跑（文档 02 §2.4）。
/// </summary>
public class DailySettlementJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        throw new NotImplementedException();
    }
}
