using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildCourt.Lua;
using WildCourt.Net;

namespace WildCourt.Core
{
    /// <summary>
    /// 游戏入口（文档 03 §3.1）：全局单例，编排启动初始化顺序——
    /// 版本检查 → 配置加载（ConfigMgr）→ 网络初始化（NetManager）→ Lua 环境（LuaManager）→ 进入场景流。
    /// <para>服务端地址在 Inspector 配置（文档 17 §17.4：联调 localhost:5000）。</para>
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("服务端（文档 17 §17.4 联调）")]
        [SerializeField] private string serverUrl = "http://localhost:5000";
        [SerializeField] private string wsUrl = "ws://localhost:5000/chat";

        [Header("热更（文档 14，腾讯云 COS + CDN）")]
        [SerializeField] private string hotfixCdnUrl = "";

        [Header("版本")]
        [SerializeField] private string appVersion = "V0.1.0-alpha";

        public string ServerUrl => serverUrl;
        public string WsUrl => wsUrl;
        public string HotfixCdnUrl => hotfixCdnUrl;
        public string AppVersion => appVersion;

        /// <summary>启动初始化是否完成。</summary>
        public bool IsReady { get; private set; }

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

        private void Start()
        {
            StartCoroutine(Boot());
        }

        private IEnumerator Boot()
        {
            // 0. 本地版本文件必须存在（文档 01 §1.7），缺失则走全量热更
            // TODO(文档 14): 接入 HotfixManager —— version.txt 对比 → manifest.json MD5 对比 → 增量下载 → LuaManager.HotReload
            Debug.Log($"[GameManager] 本地版本 {appVersion}");

            // 1. 配置表加载（StreamingAssets/config/）
            ConfigMgr.Instance.LoadAll();

            // 2. 网络初始化（HTTP 短连接 + SignalR 长连接）
            NetManager.Instance.Init(serverUrl, wsUrl);

            // 3. Lua 环境（需在 Player Settings 定义 USE_XLUA 后才启用 XLua 宿主，见 client/README.md）
            LuaManager.Instance.Init();

            IsReady = true;
            EventBus.Publish(new GameReadyEvent());
            Debug.Log("[GameManager] 初始化完成，进入场景流（Launch → ProvinceSelect → Main）");
        }

        /// <summary>切换场景（Launch / ProvinceSelect / Main / Yard ...，文档 03 §3.3）。</summary>
        public void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
