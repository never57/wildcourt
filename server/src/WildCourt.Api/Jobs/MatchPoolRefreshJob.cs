using Quartz;

namespace WildCourt.Api.Jobs;

/// <summary>
/// 匹配池刷新任务：按 hospitality_values:{provinceId} 重建 match_pool:{provinceId} 与 match_pool:{provinceId}:{cityId}（30min 或好客值变更触发）（文档 02 §2.4）。
/// </summary>
public class MatchPoolRefreshJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        throw new NotImplementedException();
    }
}
