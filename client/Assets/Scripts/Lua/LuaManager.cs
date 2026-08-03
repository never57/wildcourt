using System;
using System.IO;
using UnityEngine;
using WildCourt.Core;

#if USE_XLUA
using XLua;
#endif

namespace WildCourt.Lua
{
    /// <summary>
    /// Lua 宿主（文档 03 §3.4 目标接口）：XLua 环境管理、热更重载、Lua↔C# 互调。
    /// <para>可热更：Lua 逻辑（StreamingAssets/lua/）、UI 脚本（lua/UI/）、活动配置（lua/Logic/Activity/）、
    /// 配置表（ConfigMgr.Reload）；不可热更：C# 核心代码（文档 14 §14.1/14.2）。</para>
    /// </summary>
    public class LuaManager : MonoBehaviour
    {
        public static LuaManager Instance { get; private set; }

        public bool IsInited { get; private set; }

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

        /// <summary>初始化 Lua 环境（GameManager 启动流程第 4 步）。</summary>
        public void Init()
        {
#if USE_XLUA
            var env = LuaEnvHolder.Env;
            IsInited = env != null;
            Debug.Log("[LuaManager] XLua 环境初始化完成");
#else
            IsInited = false;
            Debug.Log("[LuaManager] 未定义 USE_XLUA，以 C# 桩模式运行（详见 client/README.md）");
#endif
        }

        /// <summary>
        /// 热更重载（文档 14 §14.3/14.4）：重载差异 Lua + 配置表重新读取。
        /// Lua 加载报错时自动回滚上一版本 Lua；连续失败 3 次提示玩家退出。
        /// </summary>
        public void HotReload()
        {
            Debug.Log("[LuaManager] HotReload 开始 ...");
            // TODO(文档 14): 
            //   1. 卸载旧 Lua 模块（luaenv.Global 中移除重载表）
            //   2. 重新 require 差异模块（失败 → 回滚上一版本，记录失败次数）
            //   3. ConfigMgr.Instance.Reload() 重读配置表
            //   4. 广播 ConfigReloadedEvent，UI 刷新
            ConfigMgr.Instance.Reload();
            EventBus.Publish(new ConfigReloadedEvent());
        }

        /// <summary>执行一段 Lua chunk。</summary>
        public void DoString(string chunk)
        {
#if USE_XLUA
            try
            {
                LuaEnvHolder.Env.DoString(chunk, "LuaManager");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LuaManager] DoString 失败: {e}");
            }
#else
            Debug.LogWarning("[LuaManager] 桩模式，忽略 DoString");
#endif
        }

        /// <summary>调用 Lua 模块函数（module 为表路径，如 "Logic.MatchMaker"）。</summary>
        public T CallFunc<T>(string module, string func, params object[] args)
        {
#if USE_XLUA
            try
            {
                var env = LuaEnvHolder.Env;
                var table = env.Global.GetInPath<LuaTable>(module);
                if (table == null)
                {
                    Debug.LogWarning($"[LuaManager] 模块不存在: {module}");
                    return default;
                }
                var luaFunc = table.GetInPath<LuaFunction>(func);
                if (luaFunc == null)
                {
                    Debug.LogWarning($"[LuaManager] 函数不存在: {module}.{func}");
                    return default;
                }
                var ret = luaFunc.Call(args);
                if (ret != null && ret.Length > 0 && ret[0] is T typed)
                {
                    return typed;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LuaManager] CallFunc {module}.{func} 失败: {e}");
            }
            return default;
#else
            Debug.LogWarning($"[LuaManager] 桩模式，忽略 CallFunc {module}.{func}");
            return default;
#endif
        }

        /// <summary>
        /// 加载配置到缓存（文档 03 §3.4/§3.5、16 §16.3）：17 张数值表已改为 JSON（Config_*.json），
        /// 统一由 ConfigMgr 只读加载，Lua 侧经 C# 注入查询，不再 require Lua 表。
        /// tableName 不含 Config_ 前缀，如 "PlayerLevel"。
        /// </summary>
        public void LoadConfig(string tableName)
        {
            ConfigMgr.Instance.GetRawJson(tableName); // 触发 C# 缓存（无则告警）
        }

        /// <summary>Lua require 自定义加载器（开发期读 Assets/LuaScripts，运行时读 StreamingAssets/lua/ + persistentDataPath）。</summary>
        private static byte[] CustomLoader(ref string filepath)
        {
            string name = filepath.Replace('.', '/') + ".lua";
            string devPath = Path.Combine(Application.dataPath, "LuaScripts", name);
            if (File.Exists(devPath)) return File.ReadAllBytes(devPath);
            string runtimePath = Path.Combine(Application.streamingAssetsPath, "lua", name);
            if (File.Exists(runtimePath)) return File.ReadAllBytes(runtimePath);
            return null;
        }
    }
}
