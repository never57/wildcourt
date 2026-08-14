using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using WildCourt.Api.Utils;

namespace WildCourt.Api.Hubs;

/// <summary>
/// SignalR 用户标识提供器：从连接查询参数 ?access_token= 解析 userId，
/// 使 ChatHub 的 Clients.User(userId) 能路由到对应用户的连接。
/// </summary>
public class SignalRUserIdProvider : IUserIdProvider
{
    private readonly ISessionStore _sessions;

    public SignalRUserIdProvider(ISessionStore sessions)
    {
        _sessions = sessions;
    }

    public string? GetUserId(HubConnectionContext connection)
    {
        var http = connection.GetHttpContext();
        var token = http?.Request.Query["access_token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        // 同步等待 token 解析（SignalR 连接建立阶段，阻塞可接受）
        var userId = _sessions.TryGetUserIdAsync(token).GetAwaiter().GetResult();
        return userId?.ToString();
    }
}
