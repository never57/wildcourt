using System;
using System.Collections;
using UnityEngine;
using WildCourt.Core;
using WildCourt.Net.Protocol;

namespace WildCourt.Net.WebSocket
{
    /// <summary>
    /// SignalR 长连接客户端（文档 02 §2.5 ChatHub / 03 §3.6 / 14 §14.4）。
    /// <para>Unity 无内置 SignalR 客户端，需二选一接入（库文件放入 Assets/Plugins/）：</para>
    /// <list type="number">
    /// <item><b>Microsoft.AspNetCore.SignalR.Client</b>（.NET Standard 2.0）——推荐，与服务端 ChatHub 直接互通；</item>
    /// <item><b>WebSocketSharp</b>——手写 JSON 事件帧协议。</item>
    /// </list>
    /// 未接入第三方库时仅打印日志（骨架保持可编译），接入后填充 <see cref="OpenConnection"/> 即可。
    /// 断线重连：指数退避，重连成功后重置计数（文档 14 §14.4：下载/连接失败处理）。
    /// </summary>
    public class SignalRClient : MonoBehaviour, IRealtimeClient
    {
        public static SignalRClient Instance { get; private set; }

        public bool IsConnected { get; private set; }

        /// <summary>收到服务端推送事件，交由 HandlerCenter 路由。</summary>
        public event Action<NetEvent, string> OnPush;

        [SerializeField] private float reconnectMinDelay = 1f;
        [SerializeField] private float reconnectMaxDelay = 30f;

        private string _wsUrl;
        private string _token;
        private bool _manualClosed;
        private int _reconnectAttempt;
        private Coroutine _reconnectRoutine;

        // TODO: 第三方连接对象（接入 SignalR Client / WebSocketSharp 后在此声明）
        // private HubConnection _connection;

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
            CloseConnection();
        }

        private void OpenConnection()
        {
            if (string.IsNullOrEmpty(_wsUrl))
            {
                Debug.LogWarning("[SignalR] wsUrl 为空，跳过实时连接（仅 HTTP）");
                return;
            }
            Debug.Log($"[SignalR] 连接 {_wsUrl} ...");

            // TODO: 建立真实连接（二选一，见类注释）。示例（Microsoft.AspNetCore.SignalR.Client）：
            //   _connection = new HubConnectionBuilder()
            //       .WithUrl(_wsUrl, o => o.AccessTokenProvider = () => Task.FromResult(_token))
            //       .Build();
            //   _connection.On<string>("LetterArrived", payload => Dispatch(NetEvent.LetterArrived, payload));
            //   _connection.On<string>("FriendApply",   payload => Dispatch(NetEvent.FriendApply, payload));
            //   _connection.On<string>("Notice",        payload => Dispatch(NetEvent.Notice, payload));
            //   _connection.On<string>("DailySettlement", payload => Dispatch(NetEvent.DailySettlement, payload));
            //   _connection.On<string>("BadgeUnlocked", payload => Dispatch(NetEvent.BadgeUnlocked, payload));
            //   _connection.On<string>("CurrencyChanged", payload => Dispatch(NetEvent.CurrencyChanged, payload));
            //   _connection.On<string>("ActivityChanged", payload => Dispatch(NetEvent.ActivityChanged, payload));
            //   _connection.StartAsync();
            //
            // 连接成功回调：IsConnected = true; _reconnectAttempt = 0; EventBus.Publish(new RealtimeConnectedEvent());
            // 连接失败/断开回调：IsConnected = false; EventBus.Publish(new RealtimeDisconnectedEvent{...}); ScheduleReconnect();
        }

        private void CloseConnection()
        {
            // TODO: _connection?.StopAsync();
            if (IsConnected)
            {
                IsConnected = false;
                EventBus.Publish(new RealtimeDisconnectedEvent { Reason = _manualClosed ? "主动断开" : "连接关闭" });
            }
        }

        /// <summary>收到服务端事件帧 → 路由分发（接入后由 Hub 回调调用）。</summary>
        private void Dispatch(NetEvent evt, string data)
        {
            OnPush?.Invoke(evt, data);
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
        }
    }
}
