using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using WildCourt.Net;

namespace WildCourt.Utils
{
    /// <summary>
    /// 头像加载公共工具：下载服务端头像 URL 并设置到目标 Image。
    /// 供 UserPanel / YardMainUI / ChatManner 等多处复用，避免重复「下载→转 Texture2D→设 sprite」逻辑。
    /// <para>内置三级缓存，避免切换界面/对话框时反复下载：
    ///   1) Sprite 缓存：命中直接赋值，不再发请求；
    ///   2) 下载中集合：同一 URL 并发去重，等首个请求完成后统一赋值；
    ///   3) 失败缓存：失败后短时间内不再重试，避免刷屏超时。</para>
    /// </summary>
    public static class AvatarLoader
    {
        // URL -> Sprite（绝对地址为 key）
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        // 正在下载的 URL -> 等待赋值的 Image 列表
        private static readonly Dictionary<string, List<Image>> _pendingImages = new Dictionary<string, List<Image>>();

        // 最近失败的 URL -> 失败时间（RealTimeSinceStartup），失败缓存窗口内不再重试
        private static readonly Dictionary<string, float> _failedCache = new Dictionary<string, float>();
        private const float FailCacheSeconds = 30f;

        /// <summary>把服务端返回的相对路径（/uploads/...）拼成绝对地址。</summary>
        public static string ToAbsoluteUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
            var baseUrl = NetManager.Instance != null ? NetManager.Instance.ServerUrl : string.Empty;
            return baseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
        }

        /// <summary>
        /// 加载头像并显示到 image。命中缓存直接赋值；否则发起下载并去重。
        /// </summary>
        public static void LoadInto(Image image, string url, MonoBehaviour runner)
        {
            if (image == null || string.IsNullOrEmpty(url) || runner == null) return;

            string fullUrl = ToAbsoluteUrl(url);

            // 1) 命中缓存：直接赋值
            if (_spriteCache.TryGetValue(fullUrl, out var cached))
            {
                image.sprite = cached;
                return;
            }

            // 2) 失败缓存窗口内：不再重试，静默跳过
            if (_failedCache.TryGetValue(fullUrl, out var failTime) &&
                Time.realtimeSinceStartup - failTime < FailCacheSeconds)
            {
                return;
            }

            // 3) 正在下载中：加入等待列表，等首个请求完成统一赋值
            if (_pendingImages.TryGetValue(fullUrl, out var list))
            {
                if (!list.Contains(image)) list.Add(image);
                return;
            }

            _pendingImages[fullUrl] = new List<Image> { image };
            runner.StartCoroutine(LoadCoroutine(fullUrl));
        }

        /// <summary>
        /// 预加载头像到图片缓存（不设置任何 Image）。用于好友界面打开时预热，
        /// 避免进入对话面板时当屏下载。命中缓存或下载中则直接返回。
        /// </summary>
        public static void Prefetch(string url, MonoBehaviour runner)
        {
            if (string.IsNullOrEmpty(url) || runner == null) return;

            string fullUrl = ToAbsoluteUrl(url);

            // 已缓存或下载中，无需重复预热
            if (_spriteCache.ContainsKey(fullUrl)) return;
            if (_failedCache.TryGetValue(fullUrl, out var failTime) &&
                Time.realtimeSinceStartup - failTime < FailCacheSeconds) return;
            if (_pendingImages.ContainsKey(fullUrl)) return;

            _pendingImages[fullUrl] = new List<Image>();
            runner.StartCoroutine(LoadCoroutine(fullUrl));
        }

        /// <summary>清空所有头像缓存（登出/刷新时调用）。</summary>
        public static void ClearCache()
        {
            _spriteCache.Clear();
            _pendingImages.Clear();
            _failedCache.Clear();
        }

        private static IEnumerator LoadCoroutine(string fullUrl)
        {
            using (var req = UnityWebRequestTexture.GetTexture(fullUrl))
            {
                req.certificateHandler = new NetManager.InsecureCertificateHandlerPublic();
                req.timeout = 10;
                // 携带会话 token：即使静态资源被鉴权中间件拦截也能通过校验
                if (NetManager.Instance != null && !string.IsNullOrEmpty(NetManager.Instance.Token))
                {
                    req.SetRequestHeader("Authorization", "Bearer " + NetManager.Instance.Token);
                }
                yield return req.SendWebRequest();

                // 取出等待列表（无论成功失败都消费掉）
                List<Image> waiters;
                _pendingImages.TryGetValue(fullUrl, out waiters);
                _pendingImages.Remove(fullUrl);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // 失败缓存：窗口内不再重试，避免切换界面刷屏超时
                    _failedCache[fullUrl] = Time.realtimeSinceStartup;
                    Debug.LogWarning($"[AvatarLoader] 头像加载失败: url={fullUrl}, error={req.error}, code={req.responseCode}");
                    yield break;
                }

                var tex = DownloadHandlerTexture.GetContent(req);
                if (tex == null)
                {
                    _failedCache[fullUrl] = Time.realtimeSinceStartup;
                    Debug.LogWarning($"[AvatarLoader] 头像纹理为空: url={fullUrl}");
                    yield break;
                }

                var sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);

                // 缓存 sprite，后续命中直接复用
                _spriteCache[fullUrl] = sprite;

                // 统一赋值给所有等待的 Image
                if (waiters != null)
                {
                    foreach (var img in waiters)
                    {
                        if (img != null) img.sprite = sprite;
                    }
                }
            }
        }
    }
}
