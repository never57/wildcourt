using System;
using System.Collections.Generic;

namespace WildCourt.Core
{
    /// <summary>
    /// 全局事件总线（文档 03 §3.7）：UI 与玩法模块通过事件解耦，
    /// 服务端快照/推送经 Net 层解析后以事件广播，各模块自行订阅。
    /// 事件类型定义见 <see cref="WildCourt.Core.Events"/>。
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            lock (Handlers)
            {
                if (Handlers.TryGetValue(typeof(T), out var existing))
                    Handlers[typeof(T)] = Delegate.Combine(existing, handler);
                else
                    Handlers[typeof(T)] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            lock (Handlers)
            {
                if (!Handlers.TryGetValue(typeof(T), out var existing)) return;
                var next = Delegate.Remove(existing, handler);
                if (next == null) Handlers.Remove(typeof(T));
                else Handlers[typeof(T)] = next;
            }
        }

        public static void Publish<T>(T evt)
        {
            Delegate d;
            lock (Handlers)
            {
                Handlers.TryGetValue(typeof(T), out d);
            }
            (d as Action<T>)?.Invoke(evt);
        }

        /// <summary>清理全部订阅（场景切换 / 重登时调用）。</summary>
        public static void Clear()
        {
            lock (Handlers)
            {
                Handlers.Clear();
            }
        }
    }
}
