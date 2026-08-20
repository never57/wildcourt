using System;
using System.Collections;
using System.Collections.Concurrent;
using WebSocketSharp;
using UnityEngine;
using WildCourt.Core;
using WildCourt.Net.Protocol;

namespace WildCourt.Net.WebSocket
{
    /// <summary>
    /// 实时长连接客户端（原生 WebSocket 实现，替代官方 SignalR 客户端）。
    /// 服务端端点：/ws/chat?access_token={token}（原生 WebSocket，帧格式 {event, data}）。
    /// 采用 WebSocketSharp（netstandard2.0，纯 C#，IL2CPP/AOT 完全兼容）。
    /// 断线重连：指数退避，重连成功后重置计数。
    ///
    /// 线程模型：WebSocketSharp 的回调（OnOpen/OnMessage/OnError/OnClose）运行在后台线程，
    /// 不能直接调用 Unity API（StartCoroutine / UI 更新）。因此回调只负责「入队」，
    /// 由 Unity 主线程的 Update() 统一取出处理，保证线程安全。
    /// </summary>
    public class SignalRClient : MonoBehaviour, IRealtimeClient
    {
        public static SignalRClient Instance { get; private set; }

        public bool IsConnected { get; private set; }

        /// <summary>收到服务端推送事件，交由 HandlerCenter 路由（主线程触发）。</summary>
        public event Action<NetEvent, string> OnPush;

        [SerializeField] private float reconnectMinDelay = 1f;
        [SerializeField] private float reconnectMaxDelay = 30f;
        [SerializeField] private float heartbeatInterval = 30f;

        private string _wsUrl;
        private string _token;
        private bool _manualClosed;
        private int _reconnectAttempt;
        private Coroutine _reconnectRoutine;
        private Coroutine _heartbeatRoutine;

        private WebSocketSharp.WebSocket _ws;

        /// <summary>后台线程 → 主线程的待处理动作队列。</summary>
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

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

        private void Update()
        {
            // 主线程统一消费后台线程入队的动作（EventBus.Publish、StartCoroutine 等）
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SignalR] 主线程处理回调异常：{ex}");
                }
            }
        }

        public void Connect(string wsUrl, string token)
        {
            _wsUrl = wsUrl;
            _token = token;
            _manualClosed = false;
            _reconnectAttempt = 0;
            OpenConnection();
        }

        public void Disconnect()
        {
            _manualClosed = true;
            StopReconnect();
            StopHeartbeat();
            CloseConnection();
        }

        private void OpenConnection()
        {
            if (string.IsNullOrEmpty(_wsUrl))
            {
                Debug.LogWarning("[SignalR] wsUrl 为空，跳过实时连接（仅 HTTP）");
                return;
            }

            // 拼接 token 鉴权参数（服务端 /ws/chat 从 ?access_token= 解析 userId）
            var url = _wsUrl;
            var sep = url.Contains("?") ? "&" : "?";
            url = $"{url}{sep}access_token={Uri.EscapeDataString(_token ?? "")}";

            Debug.Log($"[SignalR] 连接 {url} ...");

            CloseConnectionCore();

            _ws = new WebSocketSharp.WebSocket(url);
            _ws.OnOpen += OnWsOpen;
            _ws.OnMessage += OnWsMessage;
            _ws.OnError += OnWsError;
            _ws.OnClose += OnWsClose;

            // 连接在后台线程执行，避免阻塞主线程
            try
            {
                _ws.ConnectAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SignalR] 连接异常：{ex.Message}");
                EnqueueMainThread(() => ScheduleReconnect());
            }
        }

        // ---- WebSocketSharp 回调（后台线程）：只入队，不直接碰 Unity API ----

        private void OnWsOpen(object sender, EventArgs e)
        {
            IsConnected = true;
            _reconnectAttempt = 0;
            Debug.Log("[SignalR] 连接成功");
            EnqueueMainThread(() =>
            {
                EventBus.Publish(new RealtimeConnectedEvent());
                StartHeartbeat();
            });
        }

        private void OnWsMessage(object sender, MessageEventArgs e)
        {
            var text = e.Data;
            if (string.IsNullOrEmpty(text)) return;
            if (text == "pong") return; // 心跳应答

            Debug.Log($"[SignalR] 收到推送：{text}");

            // 解析帧：{ "event": "ReceivePrivateMessage", "data": "{...}" }
            try
            {
                var frame = JsonUtility.FromJson<RealtimeFrameDto>(text);
                if (frame == null || string.IsNullOrEmpty(frame.@event)) return;

                var netEvent = ParseEvent(frame.@event);
                var data = frame.data;
                EnqueueMainThread(() => OnPush?.Invoke(netEvent, data));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SignalR] 解析推送帧失败：{ex.Message}");
            }
        }

        private void OnWsError(object sender, ErrorEventArgs e)
        {
            IsConnected = false;
            Debug.LogWarning($"[SignalR] 连接错误：{e.Message}");
            EnqueueMainThread(() =>
            {
                EventBus.Publish(new RealtimeDisconnectedEvent { Reason = e.Message });
                ScheduleReconnect();
            });
        }

        private void OnWsClose(object sender, CloseEventArgs e)
        {
            IsConnected = false;
            Debug.LogWarning($"[SignalR] 连接断开：{e.Reason}");
            EnqueueMainThread(() =>
            {
                EventBus.Publish(new RealtimeDisconnectedEvent { Reason = e.Reason });
                StopHeartbeat();
                ScheduleReconnect();
            });
        }

        private void EnqueueMainThread(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        /// <summary>把服务端事件名映射到 NetEvent 枚举。</summary>
        private static NetEvent ParseEvent(string eventName)
        {
            switch (eventName)
            {
                case "LetterArrived": return NetEvent.LetterArrived;
                case "FriendApply": return NetEvent.FriendApply;
                case "ReceivePrivateMessage": return NetEvent.PrivateMessage;
                case "Notice": return NetEvent.Notice;
                case "DailySettlement": return NetEvent.DailySettlement;
                case "BadgeUnlocked": return NetEvent.BadgeUnlocked;
                case "CurrencyChanged": return NetEvent.CurrencyChanged;
                case "ActivityChanged": return NetEvent.ActivityChanged;
                default: return NetEvent.Notice;
            }
        }

        private void StartHeartbeat()
        {
            if (_heartbeatRoutine != null) return;
            _heartbeatRoutine = StartCoroutine(HeartbeatRoutine());
        }

        private IEnumerator HeartbeatRoutine()
        {
            while (IsConnected)
            {
                yield return new WaitForSeconds(heartbeatInterval);
                if (_ws != null && _ws.IsAlive)
                {
                    try
                    {
                        _ws.Send("ping");
                    }
                    catch
                    {
                        // 发送失败由 OnError/OnClose 处理
                    }
                }
            }
        }

        private void StopHeartbeat()
        {
            if (_heartbeatRoutine != null)
            {
                StopCoroutine(_heartbeatRoutine);
                _heartbeatRoutine = null;
            }
        }

        private void CloseConnection()
        {
            CloseConnectionCore();
            if (IsConnected)
            {
                IsConnected = false;
                EventBus.Publish(new RealtimeDisconnectedEvent { Reason = _manualClosed ? "主动断开" : "连接关闭" });
            }
        }

        private void CloseConnectionCore()
        {
            if (_ws != null)
            {
                _ws.OnOpen -= OnWsOpen;
                _ws.OnMessage -= OnWsMessage;
                _ws.OnError -= OnWsError;
                _ws.OnClose -= OnWsClose;
                var ws = _ws;
                _ws = null;
                try
                {
                    // WebSocketSharp 的 Close() 是同步的，会发送关闭帧并等待握手（默认超时约 5s）。
                    // 放到后台线程执行，避免阻塞 Unity 主线程导致「退出登录卡顿」。
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try { ws.Close(); } catch { /* 忽略关闭异常 */ }
                    });
                }
                catch
                {
                    // 忽略调度异常
                }
            }
        }

        private void ScheduleReconnect()
        {
            if (_manualClosed || _reconnectRoutine != null) return;
            _reconnectRoutine = StartCoroutine(ReconnectRoutine());
        }

        private IEnumerator ReconnectRoutine()
        {
            float delay = Mathf.Min(reconnectMinDelay * Mathf.Pow(2f, _reconnectAttempt), reconnectMaxDelay);
            _reconnectAttempt++;
            Debug.Log($"[SignalR] {delay:F1}s 后重连（第 {_reconnectAttempt} 次）...");
            yield return new WaitForSeconds(delay);
            _reconnectRoutine = null;
            if (!_manualClosed) OpenConnection();
        }

        private void StopReconnect()
        {
            if (_reconnectRoutine != null)
            {
                StopCoroutine(_reconnectRoutine);
                _reconnectRoutine = null;
            }
        }

        private void OnDestroy()
        {
            StopReconnect();
            StopHeartbeat();
            CloseConnectionCore();
        }

        /// <summary>服务端推送帧结构：{ "event": "...", "data": "{...}" }。data 为已序列化的负载 JSON 字符串。</summary>
        [Serializable]
        private class RealtimeFrameDto
        {
            public string @event;
            public string data;
        }
    }
}
