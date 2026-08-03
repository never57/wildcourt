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

        public string ServerUrl { get; private set; } = "http://43.138.133.137:5000";
        public string Token { get; private set; }

        [SerializeField] private float timeoutSeconds = 10f;
        [SerializeField] private int maxRetry = 1;

        private IRealtimeClient _realtime;

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

        /// <summary>初始化（文档 17 §17.4 联调：localhost:5000）。</summary>
        public void Init(string serverUrl, string wsUrl)
        {
            ServerUrl = serverUrl;
            DefaultHandlers.RegisterAll();
            _realtime = GetComponent<SignalRClient>() ?? gameObject.AddComponent<SignalRClient>();
            ConnectRealtime(wsUrl);
        }

        /// <summary>登录后注入会话 token。</summary>
        public void SetToken(string token)
        {
            Token = token;
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

        private IEnumerator SendRequest<T>(string method, string path, string bodyJson,
            Action<ApiResponse<T>> onSuccess, Action<int, string> onError, int retryLeft)
        {
            using (UnityWebRequest req = new UnityWebRequest(ServerUrl + path, method))
            {
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

        private void ConnectRealtime(string wsUrl)
        {
            if (_realtime == null || string.IsNullOrEmpty(wsUrl)) return;
            _realtime.OnPush += HandlerCenter.Instance.Route;
            _realtime.Connect(wsUrl, Token);
        }
    }
}
