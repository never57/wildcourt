using Microsoft.AspNetCore.SignalR;

namespace WlidCourt.Api.Hubs;

/// <summary>
/// 聊天与事件推送 Hub：私聊 + 新信件、新通知、结算播报（文档 02 §2.5）。
/// 客户端经 Net/WebSocket 连接。
/// </summary>
public class ChatHub : Hub
{
    /// <summary>私聊消息</summary>
    public Task SendPrivateMessage(string toUserId, string message)
    {
        throw new NotImplementedException();
    }

    /// <summary>推送事件给指定用户</summary>
    public Task NotifyUser(string userId, string eventName, object payload)
    {
        throw new NotImplementedException();
    }
}
