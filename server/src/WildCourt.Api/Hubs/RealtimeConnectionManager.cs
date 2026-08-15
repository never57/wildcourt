using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WildCourt.Api.Hubs;

/// <summary>
/// 实时连接管理器（替代 SignalR）：
/// 管理 userId → WebSocket 连接映射，支持服务端向指定用户推送 JSON 消息。
/// 客户端经 /ws/chat 建立原生 WebSocket 长连接（token 鉴权）。
/// 一个用户可有多条连接（多端在线），推送时全部投递，任一失败即清理。
/// </summary>
public class RealtimeConnectionManager
{
    /// <summary>与 REST API 一致的 camelCase 序列化（对齐客户端小写字段 DTO）。
    /// UnsafeRelaxedJsonEscaping：中文等非 ASCII 字符不转义为 \uXXXX，直接输出 UTF-8，避免客户端乱码。</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>userId → 该用户的所有活跃 WebSocket 连接。</summary>
    private readonly ConcurrentDictionary<long, ConcurrentBag<WebSocket>> _connections = new();

    /// <summary>注册连接。</summary>
    public void Register(long userId, WebSocket socket)
    {
        var bag = _connections.GetOrAdd(userId, _ => new ConcurrentBag<WebSocket>());
        bag.Add(socket);
    }

    /// <summary>注销连接（客户端断开/异常时调用）。</summary>
    public void Unregister(long userId, WebSocket socket)
    {
        if (_connections.TryGetValue(userId, out var bag))
        {
            var remaining = new List<WebSocket>();
            foreach (var s in bag)
            {
                if (!ReferenceEquals(s, socket))
                {
                    remaining.Add(s);
                }
            }
            if (remaining.Count == 0)
            {
                _connections.TryRemove(userId, out _);
            }
            else
            {
                _connections[userId] = new ConcurrentBag<WebSocket>(remaining);
            }
        }
    }

    /// <summary>
    /// 推送一条文本消息给指定用户。消息格式（data 为已序列化的 JSON 字符串，方便客户端直接路由）：
    /// { "event": "ReceivePrivateMessage", "data": "{...}" }
    /// </summary>
    public async Task SendAsync(long userId, string eventName, object payload)
    {
        if (!_connections.TryGetValue(userId, out var bag))
        {
            return;
        }

        var dataJson = payload as string ?? JsonSerializer.Serialize(payload, _jsonOptions);
        var frame = JsonSerializer.Serialize(new RealtimeFrame
        {
            Event = eventName,
            Data = dataJson,
        }, _jsonOptions);

        var bytes = Encoding.UTF8.GetBytes(frame);
        var segment = new ArraySegment<byte>(bytes);

        foreach (var socket in bag.ToArray())
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    Unregister(userId, socket);
                }
            }
            catch
            {
                Unregister(userId, socket);
            }
        }
    }

    /// <summary>推送的帧结构（客户端据此路由到对应事件）。Data 为已序列化的 payload JSON 字符串。</summary>
    private sealed class RealtimeFrame
    {
        public string Event { get; set; } = "";
        public string? Data { get; set; }
    }
}
