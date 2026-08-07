using System;
using WildCourt.Net.Protocol;

namespace WildCourt.Net.WebSocket
{
    /// <summary>
    /// 实时长连接抽象（文档 02 §2.5 ChatHub、03 §3.6）：
    /// 服务端 SignalR 推送的实时事件（私聊、通知、结算播报、信件送达）经此进入 <see cref="WildCourt.Net.Handler.HandlerCenter"/> 路由。
    /// 具体实现见 <see cref="SignalRClient"/>。
    /// </summary>
    public interface IRealtimeClient
    {
        bool IsConnected { get; }

        /// <summary>收到服务端推送事件（NetEvent + 负载 JSON）。</summary>
        event Action<NetEvent, string> OnPush;

        /// <summary>建立连接（ws://host/chat，带 token 鉴权）。</summary>
        void Connect(string wsUrl, string token);

        /// <summary>主动断开（登出/切账号）。</summary>
        void Disconnect();
    }
}
