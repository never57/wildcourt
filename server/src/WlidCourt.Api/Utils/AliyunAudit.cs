namespace WlidCourt.Api.Utils;

/// <summary>
/// 阿里云内容安全客户端封装（文字/图片）（文档 02 §2.7）。
/// </summary>
public class AliyunAudit
{
    /// <summary>文字审核：返回是否通过</summary>
    public Task<bool> CheckTextAsync(string content)
    {
        throw new NotImplementedException();
    }

    /// <summary>图片审核：返回是否通过</summary>
    public Task<bool> CheckImageAsync(string url)
    {
        throw new NotImplementedException();
    }
}
