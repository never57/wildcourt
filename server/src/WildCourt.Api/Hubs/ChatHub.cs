using Microsoft.AspNetCore.SignalR;

namespace WildCourt.Api.Hubs;

/// <summary>
/// 聊天与事件推送 Hub：私聊 + 新信件、新通知、结算播报（文档 02 §2.5）。
/// 客户端经 Net/WebSocket 连接；用户映射由 <see cref="SignalRUserIdProvider"/> 提供（?access_token=）。
/// 私聊发送走 REST（POST /api/v1/chat/send）入库 + 本 Hub 推送给接收者。
/// </summary>
public class ChatHub : Hub
{
    /// <summary>私聊消息（备用：客户端直接经 Hub 发，服务端转发；生产主链路走 REST 入库 + 推送）。</summary>
    public async Task SendPrivateMessage(long toUserId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // 当前连接用户 ID（由 SignalRUserIdProvider 解析 access_token 得到）
        var fromUserId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(fromUserId))
        {
            return;
        }

        await Clients.User(toUserId.ToString())
            .SendAsync("ReceivePrivateMessage", new
            {
                FromUserId = long.Parse(fromUserId),
                ToUserId = toUserId,
                Content = message,
            });
    }

    /// <summary>推送事件给指定用户（服务端广播入口）。</summary>
    public async Task NotifyUser(string userId, string eventName, object payload)
    {
        await Clients.User(userId).SendAsync(eventName, payload);
    }
}
