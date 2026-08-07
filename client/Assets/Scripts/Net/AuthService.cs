using System;
using UnityEngine;
using WildCourt.Net;
using WildCourt.Net.Protocol;

namespace WlidCourt.UI
{
    /// <summary>
    /// 认证服务：封装登录/注册 API 请求（.NET 10 Web API：api/v1/user/*）。
    /// 密码约定：客户端 SHA256 一次哈希后传输（防明文泄露），服务端再做 bcrypt 加盐存储；
    /// 登录成功返回会话 token，注入 <see cref="NetManager.Token"/>，后续请求自动携带 Bearer 头。
    /// </summary>
    public class AuthService : UnityEngine.MonoBehaviour
    {
        /// <summary>登录：{ username, password(SHA256) } → { token, userId }。</summary>
        public void Login(string username, string password, Action<string> onSuccess, Action<string> onError)
        {
            EnsureNetManager();

            var body = new LoginRequest
            {
                username = username.Trim(),
                password = HashPassword(password),
            };

            NetManager.Instance.Post<LoginResponse>(ApiPaths.UserLogin, body,
                onSuccess: res =>
                {
                    NetManager.Instance.SetToken(res.data.token);
                    onSuccess?.Invoke(res.data.token);
                },
                onError: (code, msg) => onError?.Invoke(msg));
        }

        /// <summary>注册：{ username, password(SHA256) } → 成功回调（服务端返回 User 基础信息，注册场景无需解析 data）。</summary>
        public void Register(string username, string password, Action onSuccess, Action<string> onError)
        {
            EnsureNetManager();

            var body = new RegisterRequest
            {
                username = username.Trim(),
                password = HashPassword(password),
            };

            // 注册接口服务端仅返回 User 基础字段（无 shell/coupon/token），
            // 故用 ApiEnvelope 只解析信封 code/message，避免误用 UserProfileResponse 造成字段对不上。
            NetManager.Instance.Post<ApiEnvelope>(ApiPaths.UserRegister, body,
                onSuccess: _ => onSuccess?.Invoke(),
                onError: (code, msg) => onError?.Invoke(msg));
        }

        /// <summary>
        /// 确保 NetManager 单例存在（start 场景当前未挂载 NetManager，此处自举；
        /// 若场景已配置则复用，DontDestroyOnLoad 保证跨场景存活）。
        /// </summary>
        private static void EnsureNetManager()
        {
            if (NetManager.Instance == null)
            {
                var go = new GameObject("NetManager");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<NetManager>();
            }
        }

        /// <summary>
        /// 客户端侧密码 SHA256（防明文网络传输）。
        /// 服务端收到后再做 bcrypt 加盐存储。
        /// </summary>
        public static string HashPassword(string raw)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
