using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if USE_XLUA
using XLua;
#endif

namespace WildCourt.Lua
{
    /// <summary>
    /// WC.timer 实现（文档 lua-api.md §5.5）：协程定时器，主线程安全。
    /// 由 <see cref="WCBridge.Bind"/> 自动创建；仅 USE_XLUA 宏开启时承载 Lua 回调。
    /// </summary>
    public class LuaTimerService : MonoBehaviour
    {
        public static LuaTimerService Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

#if USE_XLUA
        private readonly Dictionary<long, Coroutine> _timers = new Dictionary<long, Coroutine>();
        private long _nextId = 1;

        /// <summary>ms 后执行 Lua 回调，返回句柄（供 clearTimeout）。</summary>
        public long SetTimeout(LuaFunction cb, double ms)
        {
            long id = _nextId++;
            _timers[id] = StartCoroutine(Run(id, cb, Mathf.Max(0f, (float)(ms / 1000.0))));
            return id;
        }

        /// <summary>取消定时器（已执行/已取消时静默）。</summary>
        public void ClearTimeout(long handle)
        {
            if (_timers.TryGetValue(handle, out var co) && co != null)
            {
                StopCoroutine(co);
            }
            _timers.Remove(handle);
        }

        private IEnumerator Run(long id, LuaFunction cb, float delay)
        {
            yield return new WaitForSeconds(delay);
            _timers.Remove(id);
            try
            {
                cb.Call();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[LuaTimerService] 定时回调异常: " + e);
            }
        }
#endif
    }
}
