using WildCourt.Api.Models;

namespace WildCourt.Api.Services;

/// <summary>
/// 好友业务：申请/接受、偷菜（30% 产量、1 次/作物/天、20 次/天、保护罩 24h）、喂食（3 次/天）（文档 02 §2.3）。
/// </summary>
public class FriendService
{
    /// <summary>好友申请</summary>
    public Task ApplyAsync(long fromUserId, long toUserId)
    {
        throw new NotImplementedException();
    }

    /// <summary>偷菜</summary>
    public Task<object> StealAsync(long userId, long targetUserId, string cropCellId)
    {
        throw new NotImplementedException();
    }

    /// <summary>喂食</summary>
    public Task FeedAsync(long userId, long targetUserId)
    {
        throw new NotImplementedException();
    }
}
