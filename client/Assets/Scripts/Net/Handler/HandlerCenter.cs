using System;
using System.Collections.Generic;
using UnityEngine;
using WildCourt.Net.Protocol;

namespace WildCourt.Net.Handler
{
    /// <summary>实时事件处理器：模块实现该接口注册到 <see cref="HandlerCenter"/>。</summary>
    public interface INetEventHandler
    {
        /// <summary>处理一条 SignalR 推送（data 为事件负载 JSON）。</summary>
        void HandleNetEvent(NetEvent evt, string data);
    }

    /// <summary>
    /// 消息分发中心（文档 03 §3.6）：按 cmd（<see cref="NetEvent"/>）路由到对应模块处理器，
    /// 例如 FriendHandler → 好友模块。处理器负责把 data 解析为业务数据并广播 <see cref="EventBus"/> 事件。
    /// </summary>
    public class HandlerCenter
    {
        public static HandlerCenter Instance { get; } = new HandlerCenter();

        private readonly Dictionary<NetEvent, List<INetEventHandler>> _handlers =
            new Dictionary<NetEvent, List<INetEventHandler>>();

        /// <summary>注册处理器（同一事件允许多个处理器）。</summary>
        public void Register(NetEvent evt, INetEventHandler handler)
        {
            if (handler == null) return;
            if (!_handlers.TryGetValue(evt, out var list))
            {
                list = new List<INetEventHandler>();
                _handlers[evt] = list;
            }
            if (!list.Contains(handler)) list.Add(handler);
        }

        public void Unregister(NetEvent evt, INetEventHandler handler)
        {
            if (!_handlers.TryGetValue(evt, out var list)) return;
            list.Remove(handler);
        }

        /// <summary>路由一条推送事件到所有注册的处理器。</summary>
        public void Route(NetEvent evt, string data)
        {
            if (!_handlers.TryGetValue(evt, out var list)) return;
            // 拷贝一份避免处理过程中注册表被修改
            var snapshot = list.ToArray();
            foreach (var handler in snapshot)
            {
                try
                {
                    handler.HandleNetEvent(evt, data);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[HandlerCenter] {evt} 处理异常: {e}");
                }
            }
        }
    }
}
