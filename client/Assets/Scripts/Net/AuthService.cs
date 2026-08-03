using System;
using System.Collections;
using UnityEngine;

namespace WlidCourt.UI
{
    /// <summary>
    /// 认证服务：封装登录/注册 API 请求。
    /// 当前阶段为本地占位（模拟 0.5s 网络延迟）；后续接入 .NET 10 Web API（AuthController）后，
    /// 将 TODO 部分替换为 NetworkManager.Request 调用。
    ///
    /// === 服务端 MySQL 对接预留 ===
    /// 服务端 AuthController 对应接口：
    ///   POST /api/auth/login     { username, password } → { token, playerId }
    ///   POST /api/auth/register  { username, password } → { success }
    ///
    /// MySQL users 表结构参考（文档 04）：
    ///   id BIGINT PK, username VARCHAR(20) UNIQUE, password_hash VARCHAR(128),
    ///   created_at DATETIME, last_login DATETIME
    ///
    /// 安全约定：
    ///   - 密码客户端仅做 SHA256 一次哈希再传输（防明文泄露），服务端再做 bcrypt 存储
    ///   - Token 由服务端生成 JWT，客户端存入 NetworkManager.Token
    ///   - 后续所有请求带 Authorization: Bearer {token}
    /// </summary>
    public class AuthService : MonoBehaviour
    {
        /// <summary>
        /// 登录。当前占位：模拟 0.5s 延迟后生成假 token。
        /// </summary>
        public void Login(string username, string password, Action<string> onSuccess, Action<string> onError)
        {
            // ========== TODO: 接入真实服务端后替换为以下代码 ==========
            // var hashedPwd = HashPassword(password);
            // var body = new { username = username, password = hashedPwd };
            // NetworkManager.Instance.Request<LoginResponse>("api/auth/login", body,
            //     res => {
            //         NetworkManager.Instance.SetToken(res.token);
            //         onSuccess?.Invoke(res.token);
            //     },
            //     err => onError?.Invoke(err));
            // =====================================================

            StartCoroutine(FakeLoginCoroutine(username, onSuccess));
        }

        /// <summary>
        /// 注册。当前占位：模拟 0.5s 延迟后成功。
        /// </summary>
        public void Register(string username, string password, Action onSuccess, Action<string> onError)
        {
            // ========== TODO: 接入真实服务端后替换为以下代码 ==========
            // var hashedPwd = HashPassword(password);
            // var body = new { username = username, password = hashedPwd };
            // NetworkManager.Instance.Request<EmptyResponse>("api/auth/register", body,
            //     _ => onSuccess?.Invoke(),
            //     err => onError?.Invoke(err));
            // =====================================================

            StartCoroutine(FakeRegisterCoroutine(onSuccess));
        }

        private IEnumerator FakeLoginCoroutine(string username, Action<string> onSuccess)
        {
            yield return new WaitForSeconds(0.5f);
            var fakeToken = "placeholder_token_" + username + "_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            onSuccess?.Invoke(fakeToken);
        }

        private IEnumerator FakeRegisterCoroutine(Action onSuccess)
        {
            yield return new WaitForSeconds(0.5f);
            onSuccess?.Invoke();
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

        // ---- 服务端响应 DTO（后续接入真实 HTTP 时使用） ----

        [Serializable]
        private class LoginResponse
        {
            public string token;
            public long playerId;
        }

        [Serializable]
        private class EmptyResponse { }
    }
}
