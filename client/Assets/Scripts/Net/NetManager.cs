using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using WildCourt.Core;
using WildCourt.Net.Handler;
using WildCourt.Net.Protocol;
using WildCourt.Net.WebSocket;
using WildCourt.Utils;

namespace WildCourt.Net
{
    /// <summary>
    /// 网络管理器（文档 03 §3.6、02 §2.2）：
    /// <list type="bullet">
    /// <item>HTTP 短连接：REST 业务接口（UnityWebRequest），统一信封解析、token 注入、网络错误重试；</item>
    /// <item>WebSocket 长连接：SignalR 实时事件（信件送达/好友申请/通知/结算播报）。</item>
    /// </list>
    /// 关键请求带幂等键（Idempotency-Key），断线重连由 <see cref="SignalRClient"/> 负责。
    /// </summary>
    public class NetManager : MonoBehaviour
    {
        public static NetManager Instance { get; private set; }

        public string ServerUrl { get; private set; }

        [Header("服务端（文档 17 §17.4 联调）")]
        [Tooltip("REST 服务地址，需与服务端端口一致（生产：http://43.138.133.137 走 nginx 80；本地 localhost:5000）")]
        [SerializeField] private string serverUrl = "http://43.138.133.137";

        [Header("实时连接")]
        [Tooltip("原生 WebSocket 长连接地址（生产：ws://43.138.133.137/ws/chat；本地 ws://localhost:5000/ws/chat）")]
        [SerializeField] private string wsUrl = "ws://43.138.133.137/ws/chat";

        public string Token { get; private set; }

        [SerializeField] private float timeoutSeconds = 10f;
        [SerializeField] private int maxRetry = 1;

        private IRealtimeClient _realtime;
        private string _wsUrl;
        private bool _initialized;

        /// <summary>是否已完成初始化（HandlerCenter 已注册、SignalRClient 已创建）。</summary>
        public bool IsInitialized => _initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // 默认取序列化配置的 serverUrl（服务端端口默认 5000）
            ServerUrl = serverUrl;
            // 自举：无论从哪个入口创建 NetManager（Splash/Login/DebugScene），都确保网络层就绪。
            // SignalR 连接本身延迟到 SetToken（登录拿到 token）后，见 ConnectRealtime。
            Init(serverUrl, wsUrl);
        }

        /// <summary>初始化（文档 17 §17.4 联调：localhost:5000）。幂等，重复调用无副作用。</summary>
        public void Init(string serverUrl, string wsUrl)
        {
            if (_initialized) return;
            ServerUrl = string.IsNullOrEmpty(serverUrl) ? ServerUrl : serverUrl;
            _wsUrl = wsUrl;
            DefaultHandlers.RegisterAll();
            _realtime = GetComponent<SignalRClient>() ?? gameObject.AddComponent<SignalRClient>();
            _realtime.OnPush += HandlerCenter.Instance.Route;
            _initialized = true;
        }

        /// <summary>登录后注入会话 token，并建立实时连接（SignalR 需 token 鉴权，故在登录后连接）。</summary>
        public void SetToken(string token)
        {
            Token = token;
            ConnectRealtime();
        }

        public void Get<T>(string path, Action<ApiResponse<T>> onSuccess, Action<int, string> onError = null)
        {
            StartCoroutine(SendRequest<T>("GET", path, null, onSuccess, onError, maxRetry));
        }

        public void Post<T>(string path, object body, Action<ApiResponse<T>> onSuccess, Action<int, string> onError = null)
        {
            PostJson<T>(path, body == null ? null : JsonUtil.ToJson(body), onSuccess, onError);
        }

        public void PostJson<T>(string path, string bodyJson, Action<ApiResponse<T>> onSuccess, Action<int, string> onError = null)
        {
            StartCoroutine(SendRequest<T>("POST", path, bodyJson, onSuccess, onError, maxRetry));
        }

        public void Put<T>(string path, object body, Action<ApiResponse<T>> onSuccess, Action<int, string> onError = null)
        {
            PutJson<T>(path, body == null ? null : JsonUtil.ToJson(body), onSuccess, onError);
        }

        public void PutJson<T>(string path, string bodyJson, Action<ApiResponse<T>> onSuccess, Action<int, string> onError = null)
        {
            StartCoroutine(SendRequest<T>("PUT", path, bodyJson, onSuccess, onError, maxRetry));
        }

        public void Delete<T>(string path, Action<ApiResponse<T>> onSuccess, Action<int, string> onError = null)
        {
            StartCoroutine(SendRequest<T>("DELETE", path, null, onSuccess, onError, maxRetry));
        }

        // ---- 原始文本请求（WC.net 桥接用：Lua 侧自行解析信封，无需预知 DTO 类型） ----

        public void GetRaw(string path, Action<string> onSuccess, Action<int, string> onError = null)
        {
            StartCoroutine(SendRaw("GET", path, null, onSuccess, onError, maxRetry));
        }

        public void PostRaw(string path, string bodyJson, Action<string> onSuccess, Action<int, string> onError = null)
        {
            StartCoroutine(SendRaw("POST", path, bodyJson, onSuccess, onError, maxRetry));
        }

        public void PutRaw(string path, string bodyJson, Action<string> onSuccess, Action<int, string> onError = null)
        {
            StartCoroutine(SendRaw("PUT", path, bodyJson, onSuccess, onError, maxRetry));
        }

        private IEnumerator SendRequest<T>(string method, string path, string bodyJson,
            Action<ApiResponse<T>> onSuccess, Action<int, string> onError, int retryLeft)
        {
            using (UnityWebRequest req = new UnityWebRequest(ServerUrl + path, method))
            {
                // 开发/联调阶段允许任何连接（含本地 HTTP 明文），规避 "Insecure connection not allowed"。
                // 生产环境应移除该兜底并改用 HTTPS + 证书校验。
                req.certificateHandler = new InsecureCertificateHandler();
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = (int)timeoutSeconds;
                req.SetRequestHeader("Accept", "application/json");
                if (!string.IsNullOrEmpty(Token))
                {
                    req.SetRequestHeader("Authorization", "Bearer " + Token);
                }
                if (bodyJson != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Idempotency-Key", Guid.NewGuid().ToString("N")); // 关键请求幂等，文档 03 §3.6
                }

                yield return req.SendWebRequest();

                // 网络层失败（断网/超时）：广播断线，重试一次
                // 注：旧版 Unity 的 UnityWebRequest.Result 无 Timeout 成员，超时归并到 ConnectionError 处理
                if (req.result == UnityWebRequest.Result.ConnectionError ||
                    req.result == UnityWebRequest.Result.ProtocolError && req.responseCode == 0)
                {
                    EventBus.Publish(new NetDisconnectedEvent { Reason = req.error });
                    if (retryLeft > 0)
                    {
                        StartCoroutine(SendRequest<T>(method, path, bodyJson, onSuccess, onError, retryLeft - 1));
                        yield break;
                    }
                    onError?.Invoke(ApiCodes.NetworkError, req.error);
                    yield break;
                }

                string text = req.downloadHandler == null ? null : req.downloadHandler.text;

                // HTTP 状态码错误（4xx/5xx）
                if (req.responseCode < 200 || req.responseCode >= 300)
                {
                    var env = string.IsNullOrEmpty(text) ? null : JsonUtil.FromJson<ApiEnvelope>(text);
                    int code = (int)req.responseCode;
                    onError?.Invoke(code, env != null && !string.IsNullOrEmpty(env.message) ? env.message : req.error);
                    yield break;
                }

                // 业务信封解析
                var envelope = string.IsNullOrEmpty(text) ? null : JsonUtil.FromJson<ApiResponse<T>>(text);
                if (envelope == null)
                {
                    onError?.Invoke(ApiCodes.ServerError, "响应解析失败");
                    yield break;
                }

                if (envelope.code == ApiCodes.Unauthorized)
                {
                    // TODO: token 失效 → 跳登录页
                }

                if (envelope.IsSuccess)
                {
                    EventBus.Publish(new NetConnectedEvent());
                    onSuccess?.Invoke(envelope);
                }
                else
                {
                    onError?.Invoke(envelope.code, envelope.message);
                }
            }
        }

        /// <summary>原始文本请求：token 注入 / 幂等 / 重试与 SendRequest 一致，成功回调返回响应文本。</summary>
        private IEnumerator SendRaw(string method, string path, string bodyJson,
            Action<string> onSuccess, Action<int, string> onError, int retryLeft)
        {
            using (UnityWebRequest req = new UnityWebRequest(ServerUrl + path, method))
            {
                req.certificateHandler = new InsecureCertificateHandler();
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = (int)timeoutSeconds;
                req.SetRequestHeader("Accept", "application/json");
                if (!string.IsNullOrEmpty(Token))
                {
                    req.SetRequestHeader("Authorization", "Bearer " + Token);
                }
                if (bodyJson != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Idempotency-Key", Guid.NewGuid().ToString("N"));
                }

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.ConnectionError ||
                    req.result == UnityWebRequest.Result.ProtocolError && req.responseCode == 0)
                {
                    EventBus.Publish(new NetDisconnectedEvent { Reason = req.error });
                    if (retryLeft > 0)
                    {
                        StartCoroutine(SendRaw(method, path, bodyJson, onSuccess, onError, retryLeft - 1));
                        yield break;
                    }
                    onError?.Invoke(ApiCodes.NetworkError, req.error);
                    yield break;
                }

                string text = req.downloadHandler == null ? null : req.downloadHandler.text;
                if (req.responseCode < 200 || req.responseCode >= 300)
                {
                    var env = string.IsNullOrEmpty(text) ? null : JsonUtil.FromJson<ApiEnvelope>(text);
                    onError?.Invoke((int)req.responseCode,
                        env != null && !string.IsNullOrEmpty(env.message) ? env.message : req.error);
                    yield break;
                }

                onSuccess?.Invoke(text ?? string.Empty);
            }
        }

        private void ConnectRealtime()
        {
            if (_realtime == null || string.IsNullOrEmpty(_wsUrl)) return;
            if (_realtime.IsConnected) return;
            _realtime.Connect(_wsUrl, Token);
        }

        /// <summary>信任所有证书的兜底 handler（仅开发/联调使用，规避 HTTP 明文被拒）。</summary>
        private sealed class InsecureCertificateHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData) => true;
        }
    }
}
