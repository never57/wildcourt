using Quartz;

namespace WlidCourt.Api.Jobs;

/// <summary>
/// 信件审核任务：消费 letter_audit_queue，逐封调 AuditService.AuditAsync（AI→人工→举报三层）（文档 02 §2.4）。
/// </summary>
public class LetterAuditJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        throw new NotImplementedException();
    }
}
