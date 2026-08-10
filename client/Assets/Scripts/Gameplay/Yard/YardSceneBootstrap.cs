using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using WildCourt.Core;
using WildCourt.Lua;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.UI.Yard;

namespace WildCourt.Gameplay.Yard
{
    /// <summary>
    /// 庭院场景引导（阶段 C）：MainScene 加载后自动搭建庭院运行时（无需场景/预制体配置）。
    /// 负责：EventSystem、YardManager、YardRenderer、YardPanel 的创建与初始数据拉取。
    /// </summary>
    public static class YardSceneBootstrap
    {
        private const string TargetScene = "MainScene";

        private static bool _booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryBoot(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBoot(scene);
        }

        private static void TryBoot(Scene scene)
        {
            if (_booted || scene.name != TargetScene) return;
            _booted = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            EnsureEventSystem();
            EnsureCamera();

            // 直接从 MainScene 播放（未经过 Splash/Login 的 GameManager.Boot）时，确保配置表已加载
            if (!ConfigMgr.Instance.IsLoaded)
            {
                ConfigMgr.Instance.LoadAll();
            }

            var host = new GameObject("YardRuntimeHost");
            var yard = host.AddComponent<YardManager>();
            host.AddComponent<YardRenderer>();
            YardPanel.CreateOrInstantiate(yard);

            // 初始数据：余额 → 背包 → 庭院布局
            var net = NetManager.Instance;
            if (net != null)
            {
                net.Get<UserProfileResponse>(ApiPaths.UserProfile,
                    resp =>
                    {
                        if (YardPanel.Instance != null) YardPanel.Instance.OnProfileLoaded(resp.data);
                    },
                    (code, msg) => Debug.LogWarning($"[Yard] profile 加载失败: {msg}"));

                net.Get<InventoryResponse>(ApiPaths.UserInventory,
                    resp =>
                    {
                        if (YardPanel.Instance != null) YardPanel.Instance.OnInventoryLoaded(resp.data);
                    },
                    (code, msg) => Debug.LogWarning($"[Yard] inventory 加载失败: {msg}"));

                yard.LoadYard();
            }
            else
            {
                Debug.LogWarning("[Yard] NetManager 不存在，跳过初始数据拉取（请从 Splash/Login 正常进入）");
            }

            // 种田 Lua 模块：恢复服务端种植并重建倒计时（WC 桥接就绪后生效；桩模式仅日志）
            if (LuaManager.Instance != null)
            {
                LuaManager.Instance.CallFunc<object>("Logic.Farm.FarmManager", "Init");
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = YardRenderer.CameraSize;
            var t = cam.transform;
            t.position = new Vector3(0f, YardRenderer.WorldCenterY, t.position.z);
        }
    }
}
