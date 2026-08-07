using System;
using UnityEngine;

/// <summary>
/// 网络层最小骨架：负责与服务端通信。
/// 对应文档十五 Scripts/Net 约定；服务端为 .NET 10 Web API（文档十五 15.2）。
/// 当前占位，后续接入真实 HTTP/WebSocket 与 AuthMiddleware（token 鉴权）。
/// </summary>
public class NetworkManager
{
    public string BaseUrl = "https://api.wildcourt.example"; // TODO: 由 appsettings/构建变量注入
    public string Token { get; private set; }

    public void SetToken(string token) => Token = token;

    /// <summary>
    /// 通用请求骨架（占位）。实际实现：拼接 BaseUrl + 路径，附带 Token 头，反序列化为 T。
    /// 对应文档九 ShopController / 文档四 YardService 等接口。
    /// </summary>
    public void Request<T>(string path, object body, Action<T> onSuccess, Action<string> onError)
    {
        // TODO: 接入 UnityEngine.Networking.UnityWebRequest 或第三方 HTTP 客户端。
        Debug.Log($"[NetworkManager] 请求 {path}（未实现）");
    }
}
