namespace WlidCourt.Api.Services;

/// <summary>
/// 信件审核业务：三层审核（阿里云 AI → 不确定转人工 → 通过/退回）（文档 02 §2.3）。
/// </summary>
public class AuditService
{
    /// <summary>审核单封信件</summary>
    public Task AuditAsync(long letterId)
    {
        throw new NotImplementedException();
    }

    /// <summary>处理举报</summary>
    public Task HandleReportAsync(long letterId)
    {
        throw new NotImplementedException();
    }
}
