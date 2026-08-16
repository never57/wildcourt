using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using WildCourt.Net;

namespace WildCourt.Utils
{
    /// <summary>
    /// 头像加载公共工具：下载服务端头像 URL 并设置到目标 Image。
    /// 供 UserPanel / YardMainUI 等多处复用，避免重复「下载→转 Texture2D→设 sprite」逻辑。
    /// </summary>
    public static class AvatarLoader
    {
        /// <summary>把服务端返回的相对路径（/uploads/...）拼成绝对地址。</summary>
        public static string ToAbsoluteUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
            var baseUrl = NetManager.Instance != null ? NetManager.Instance.ServerUrl : string.Empty;
            return baseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
        }

        /// <summary>加载头像并显示到 image（image 为 null 或 url 为空则直接返回）。</summary>
        public static void LoadInto(Image image, string url, MonoBehaviour runner)
        {
            if (image == null || string.IsNullOrEmpty(url) || runner == null) return;
            runner.StartCoroutine(LoadCoroutine(image, url));
        }

        private static IEnumerator LoadCoroutine(Image image, string url)
        {
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                req.certificateHandler = new NetManager.InsecureCertificateHandlerPublic();
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[AvatarLoader] 头像加载失败: {req.error}");
                    yield break;
                }

                var tex = DownloadHandlerTexture.GetContent(req);
                if (tex != null && image != null)
                {
                    image.sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }
    }
}
