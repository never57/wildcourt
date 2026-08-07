using UnityEngine;

#if USE_XLUA
using XLua;
#endif

namespace WildCourt.Lua
{
    /// <summary>
    /// Lua 面板生命周期桥（文档 03 §3.7）：挂在 UI 预制体上，绑定 Lua 表的
    /// OnInit / OnOpen / OnClose / OnDispose 方法。UI 脚本可热更（lua/UI/，文档 14 §14.1）。
    /// </summary>
    public class LuaBehaviour : MonoBehaviour
    {
        [Tooltip("Lua 模块路径，如 UI.YardPanel（对应 lua/UI/YardPanel.lua）")]
        [SerializeField] private string luaModule;

#if USE_XLUA
        private LuaTable _luaTable;
#endif

        /// <summary>运行时绑定模块（可在代码中设置后调用）。</summary>
        public void Bind(string module)
        {
            luaModule = module;
#if USE_XLUA
            if (string.IsNullOrEmpty(luaModule)) return;
            var env = LuaEnvHolder.Env;
            if (env == null) return;
            _luaTable = env.Global.GetInPath<LuaTable>(luaModule);
            if (_luaTable == null)
            {
                Debug.LogWarning($"[LuaBehaviour] 找不到 Lua 模块: {luaModule}");
            }
#endif
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(luaModule)) return;
            Bind(luaModule);
            CallLuaFunc("OnInit");
        }

        private void OnEnable()
        {
            CallLuaFunc("OnOpen");
        }

        private void OnDisable()
        {
            CallLuaFunc("OnClose");
        }

        private void OnDestroy()
        {
            CallLuaFunc("OnDispose");
#if USE_XLUA
            _luaTable = null;
#endif
        }

        private void CallLuaFunc(string funcName)
        {
#if USE_XLUA
            if (_luaTable == null) return;
            var func = _luaTable.GetInPath<LuaFunction>(funcName);
            func?.Call();
#else
            // 桩模式：仅日志，便于未装 XLua 时开发 UI
            if (!string.IsNullOrEmpty(luaModule))
            {
                Debug.Log($"[LuaBehaviour] 桩模式: {luaModule}.{funcName}");
            }
#endif
        }
    }
}
