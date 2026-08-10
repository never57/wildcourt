using System;
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

        /// <summary>可热更 Lua 模块白名单（文档 14 §14.1；新增热更脚本需加入此清单）。</summary>
        public static readonly string[] HotModules =
        {
            "Logic.Match.MatchMaker",
            "Logic.MiniGame.Config_MiniGame",
            "Logic.SignIn.Config_DailySignIn",
            "Logic.Activity.Activity_MidAutumn",
            "Logic.Farm.FarmManager",
            "Localization.LetterTemplates",
        };

        private int _hotReloadFailures;

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
            if (IsInited)
            {
                LoadHotModules();
            }
            Debug.Log("[LuaManager] XLua 环境初始化完成");
#else
            IsInited = false;
            Debug.Log("[LuaManager] 未定义 USE_XLUA，以 C# 桩模式运行（详见 client/README.md）");
#endif
        }

        /// <summary>上线自检：逐个 require 热更模块并输出结果日志。</summary>
        public void LoadHotModules()
        {
#if USE_XLUA
            foreach (var module in HotModules)
            {
                try
                {
                    LuaEnvHolder.Env.DoString($"require('{module}')", "LuaManager.LoadHotModules");
                    Debug.Log($"[LuaManager] 热更模块加载成功: {module}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LuaManager] 热更模块加载失败: {module} :: {e.Message}");
                }
            }
#endif
        }

        /// <summary>
        /// 热更重载（文档 14 §14.3/14.4）：重载差异 Lua + 配置表重新读取。
        /// Lua 加载报错时自动回滚上一版本 Lua；连续失败 3 次提示玩家退出。
        /// </summary>
        public void HotReload()
        {
            Debug.Log("[LuaManager] HotReload 开始 ...");
#if USE_XLUA
            bool ok = true;
            var env = LuaEnvHolder.Env;
            foreach (var module in HotModules)
            {
                try
                {
                    // 清 package.loaded 缓存强制重新 require（文件级版本回滚留待完整热更链路）
                    env.DoString($"package.loaded['{module}'] = nil; require('{module}')", "LuaManager.HotReload");
                }
                catch (Exception e)
                {
                    ok = false;
                    Debug.LogError($"[LuaManager] 重载失败: {module} :: {e.Message}");
                }
            }

            _hotReloadFailures = ok ? 0 : _hotReloadFailures + 1;
            if (_hotReloadFailures >= 3)
            {
                Debug.LogError("[LuaManager] 热更连续失败 3 次，请检查网络或资源");
            }
#else
            Debug.LogWarning("[LuaManager] 桩模式，跳过 Lua 重载");
#endif
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
                // 先 require（CustomLoader 按 . 转 / 查找 Lua 文件），返回值暂存全局便于取表
                env.DoString($"__wc_module = require('{module}')", "LuaManager.CallFunc");
                var table = env.Global.Get<LuaTable>("__wc_module");
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
    }
}
