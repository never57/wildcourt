using System;
using System.Collections.Generic;
using UnityEngine;

#if USE_XLUA
using XLua;
#endif

namespace WildCourt.Lua
{
    /// <summary>
    /// WC.event 的 C# 侧注册表（文档 lua-api.md §3）：Lua 与 C# 回调按事件名并存。
    /// 仅 USE_XLUA 宏开启时生效；未开启时保持桩模式（事件无消费者）。
    /// </summary>
    public static class LuaEventBridge
    {
#if USE_XLUA
        private sealed class Entry
        {
            public long Id;
            public Action<LuaTable> Callback;
        }

        private static readonly Dictionary<string, List<Entry>> Events = new();
        private static long _nextId = 1;

        /// <summary>Lua 侧订阅（WC.event.on），返回句柄供 off 使用。</summary>
        public static long On(string name, LuaFunction cb)
        {
            long id = _nextId++;
            Add(name, new Entry { Id = id, Callback = t => cb.Call(t) });
            return id;
        }

        /// <summary>Lua 侧取消订阅（WC.event.off）。</summary>
        public static void Off(string name, long handle)
        {
            if (!Events.TryGetValue(name, out var list)) return;
            list.RemoveAll(e => e.Id == handle);
            if (list.Count == 0) Events.Remove(name);
        }

        /// <summary>C# 侧订阅（FarmPanel 等使用）。</summary>
        public static void Subscribe(string name, Action<LuaTable> callback)
        {
            Add(name, new Entry { Id = 0, Callback = callback });
        }

        /// <summary>发布事件（Lua 侧 WC.event.publish 与 C# 侧共用）。</summary>
        public static void Publish(string name, LuaTable payload)
        {
            if (!Events.TryGetValue(name, out var list) || list.Count == 0) return;
            var snapshot = list.ToArray();
            foreach (var e in snapshot)
            {
                try
                {
                    e.Callback(payload);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LuaEventBridge] 事件回调异常 {name}: {ex}");
                }
            }
        }

        private static void Add(string name, Entry entry)
        {
            if (!Events.TryGetValue(name, out var list))
            {
                list = new List<Entry>();
                Events[name] = list;
            }
            list.Add(entry);
        }
#endif
    }
}
