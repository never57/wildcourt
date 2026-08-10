using UnityEngine;
using UnityEngine.SceneManagement;
using WildCourt.Core;
using WildCourt.Lua;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.UI;

namespace WildCourt.Gameplay.Crop
{
    /// <summary>
    /// DebugScene 自动登录引导（开发用）：进入 Play Mode 时自动注册/登录测试账号并注入 token，
    /// 让开发者无需登录 UI 即可在 DebugScene 直接做调试/联调（例如跑 FarmDebug 种田链路、接口联调、庭院调试）。
    /// <para>流程：确保 NetManager/ConfigMgr/LuaManager 就绪 → 登录测试账号
    /// （不存在则先注册）→ SetToken → 触发 <see cref="OnLoggedIn"/>。</para>
    /// <para>使用：Play DebugScene 即可；改账号改 <see cref="TestUsername"/> / <see cref="TestPassword"/>，
    /// 不想自动登录把 <see cref="Enabled"/> 设为 false。</para>
    /// </summary>
    public static class DebugSceneAutoLogin
    {
        private const string TargetScene = "DebugScene";

        /// <summary>登录成功后目标服务端（联调线上；本地调试可改为 http://localhost:5000）。</summary>
        private const string ServerUrl = "http://43.138.133.137:5000";

        /// <summary>测试账号（不存在时自动注册；密码为开发约定值，勿用于正式环境）。</summary>
        private const string TestUsername = "wc_debug";
        private const string TestPassword = "debug123456";

        /// <summary>设为 false 可临时关闭自动登录。</summary>
        public static bool Enabled = true;

        /// <summary>登录成功回调（token），供 FarmDebug 等脚本挂接。</summary>
        public static event System.Action<string> OnLoggedIn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (!Enabled) return;
            if (SceneManager.GetActiveScene().name != TargetScene) return;
            Boot();
        }

        private static void Boot()
        {
            // 1) 运行时搭建：NetManager → 配置表 → Lua 环境（WC 桥接）
            if (NetManager.Instance == null)
            {
                var go = new GameObject("NetManager");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<NetManager>();
            }
            NetManager.Instance.Init(ServerUrl, null);

            if (!ConfigMgr.Instance.IsLoaded)
            {
                ConfigMgr.Instance.LoadAll();
            }

            if (LuaManager.Instance == null)
            {
                var go = new GameObject("LuaManager");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<LuaManager>();
            }
            LuaManager.Instance.Init();

            Debug.Log($"[DebugSceneAutoLogin] 运行时就绪 server={ServerUrl}，开始自动登录 {TestUsername} ...");
            LoginOrRegister(attempt: 0);
        }

        /// <summary>登录测试账号；未注册（401）则注册后重登；注册冲突（409）直接重登。</summary>
        private static void LoginOrRegister(int attempt)
        {
            var net = NetManager.Instance;
            var loginBody = new LoginRequest
            {
                username = TestUsername,
                password = AuthService.HashPassword(TestPassword),
            };

            net.Post<LoginResponse>(ApiPaths.UserLogin, loginBody,
                onSuccess: resp =>
                {
                    net.SetToken(resp.data.token);
                    Debug.Log($"[DebugSceneAutoLogin] 登录成功 user={TestUsername} userId={resp.data.userId}");
                    Debug.Log("[DebugSceneAutoLogin] 可执行调试 Lua（如 require('Logic.Farm.FarmDebug') 后调 Init()/Plant()）");
                    OnLoggedIn?.Invoke(resp.data.token);
                },
                onError: (code, msg) =>
                {
                    if (code == ApiCodes.Unauthorized && attempt == 0)
                    {
                        Debug.Log($"[DebugSceneAutoLogin] 账号不存在（{msg}），自动注册后重登...");
                        var registerBody = new RegisterRequest
                        {
                            username = TestUsername,
                            password = AuthService.HashPassword(TestPassword),
                        };
                        net.Post<ApiEnvelope>(ApiPaths.UserRegister, registerBody,
                            onSuccess: _ => LoginOrRegister(1),
                            onError: (rCode, rMsg) =>
                            {
                                if (rCode == ApiCodes.Conflict)
                                {
                                    Debug.Log($"[DebugSceneAutoLogin] 注册冲突（{rMsg}），直接重登...");
                                    LoginOrRegister(1);
                                }
                                else
                                {
                                    Debug.LogError($"[DebugSceneAutoLogin] 注册失败 code={rCode} msg={rMsg}");
                                }
                            });
                    }
                    else
                    {
                        Debug.LogError($"[DebugSceneAutoLogin] 登录失败 code={code} msg={msg}");
                    }
                });
        }
    }
}
