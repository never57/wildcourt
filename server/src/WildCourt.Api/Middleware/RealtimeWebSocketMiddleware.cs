using System.Net.WebSockets;
using System.Text;
using WildCourt.Api.Hubs;
using WildCourt.Api.Utils;

namespace WildCourt.Api.Middleware;

/// <summary>
/// 实时长连接中间件（替代 SignalR）：处理 /ws/chat 的原生 WebSocket 连接。
/// 握手：GET /ws/chat?access_token={token}，服务端校验 token 得到 userId 后升级连接。
/// 之后：服务端可主动推送；客户端可发送心跳 ping（服务端回 pong）保持连接。
/// </summary>
public class RealtimeWebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISessionStore _sessions;

    public RealtimeWebSocketMiddleware(RequestDelegate next, ISessionStore sessions)
    {
        _next = next;
        _sessions = sessions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.Equals("/ws/chat", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // 从 ?access_token= 解析 token → userId
        var token = context.Request.Query["access_token"].ToString();
        var userId = await _sessions.TryGetUserIdAsync(token);
        if (!userId.HasValue)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var manager = context.RequestServices.GetRequiredService<RealtimeConnectionManager>();
        manager.Register(userId.Value, socket);

        try
        {
            await ReceiveLoop(socket);
        }
        finally
        {
            manager.Unregister(userId.Value, socket);
        }
    }

    /// <summary>接收循环：处理心跳 ping（回 pong），忽略其他入站消息（业务发送走 REST）。</summary>
    private static async Task ReceiveLoop(WebSocket socket)
    {
        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            catch (WebSocketException)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim();
                // 心跳：收到 ping 回 pong，用于客户端保活与服务端探活
                if (text.Equals("ping", StringComparison.OrdinalIgnoreCase))
                {
                    var pong = Encoding.UTF8.GetBytes("pong");
                    await socket.SendAsync(new ArraySegment<byte>(pong), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }
}
