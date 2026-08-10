using WildCourt.Api.Models;

namespace WildCourt.Api.Services;

/// <summary>
/// 信件业务：状态机（撰写中→待审核→通过/退回→送达）、回赠、送达（文档 02 §2.3）。
/// </summary>
public class LetterService
{
    /// <summary>写信（入 letter_audit_queue）</summary>
    public Task<long> SendAsync(long fromUserId, long toUserId, string content, string? nonfictionId)
    {
        throw new NotImplementedException();
    }

    /// <summary>收件箱</summary>
    public Task<List<Letter>> InboxAsync(long userId)
    {
        throw new NotImplementedException();
    }

    /// <summary>回赠（好友免审）</summary>
    public Task GiftBackAsync(long letterId, string nonfictionId, string content)
    {
        throw new NotImplementedException();
    }
}
