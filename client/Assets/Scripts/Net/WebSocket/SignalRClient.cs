using System;
using System.Collections;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;
using WildCourt.Core;
using WildCourt.Net.Protocol;

namespace WildCourt.Net.WebSocket
{
    /// <summary>
    /// SignalR 长连接客户端（文档 02 §2.5 ChatHub / 03 §3.6 / 14 §14.4）。
    /// 基于 Microsoft.AspNetCore.SignalR.Client（.NET Standard 2.0，dll 在 Assets/Plugins/SignalR/）。
    /// 断线重连：指数退避，重连成功后重置计数。
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

        private HubConnection _connection;

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

            // 释放旧连接（重连时避免泄漏）
            CloseConnectionCore();

            _connection = new HubConnectionBuilder()
                .WithUrl(_wsUrl, options =>
                {
                    // 服务端 SignalRUserIdProvider 从 ?access_token= 解析用户
                    options.AccessTokenProvider = () => Task.FromResult(_token);
                })
                .Build();

            // 服务端推送事件注册（payload 为 JSON 字符串，交由 HandlerCenter 反序列化）
            _connection.On<string>("ReceivePrivateMessage", payload => Dispatch(NetEvent.PrivateMessage, payload));
            _connection.On<string>("LetterArrived", payload => Dispatch(NetEvent.LetterArrived, payload));
            _connection.On<string>("FriendApply", payload => Dispatch(NetEvent.FriendApply, payload));
            _connection.On<string>("Notice", payload => Dispatch(NetEvent.Notice, payload));
            _connection.On<string>("DailySettlement", payload => Dispatch(NetEvent.DailySettlement, payload));
            _connection.On<string>("BadgeUnlocked", payload => Dispatch(NetEvent.BadgeUnlocked, payload));
            _connection.On<string>("CurrencyChanged", payload => Dispatch(NetEvent.CurrencyChanged, payload));
            _connection.On<string>("ActivityChanged", payload => Dispatch(NetEvent.ActivityChanged, payload));

            // 断线回调（被动断开 → 自动重连）
            _connection.Closed += OnConnectionClosed;

            StartAsync(_connection);
        }

        private async void StartAsync(HubConnection connection)
        {
            try
            {
                await connection.StartAsync();
                IsConnected = true;
                _reconnectAttempt = 0;
                Debug.Log("[SignalR] 连接成功");
                EventBus.Publish(new RealtimeConnectedEvent());
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Debug.LogWarning($"[SignalR] 连接失败：{ex.Message}");
                EventBus.Publish(new RealtimeDisconnectedEvent { Reason = ex.Message });
                ScheduleReconnect();
            }
        }

        private Task OnConnectionClosed(Exception ex)
        {
            IsConnected = false;
            Debug.LogWarning($"[SignalR] 连接断开：{ex?.Message ?? "remote"}");
            EventBus.Publish(new RealtimeDisconnectedEvent { Reason = ex?.Message ?? "连接断开" });
            ScheduleReconnect();
            return Task.CompletedTask;
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
            if (_connection != null)
            {
                _connection.Closed -= OnConnectionClosed;
                try
                {
                    _ = _connection.DisposeAsync();
                }
                catch
                {
                    // 忽略释放异常
                }
                _connection = null;
            }
        }

        /// <summary>收到服务端事件帧 → 路由分发。</summary>
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
