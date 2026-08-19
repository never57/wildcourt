using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using WildCourt.Net;

namespace WildCourt.Utils
{
    /// <summary>
    /// 头像加载公共工具：下载服务端头像 URL 并设置到目标 Image。
    /// 供 UserPanel / YardMainUI / ChatManner 等多处复用，避免重复「下载→转 Texture2D→设 sprite」逻辑。
    /// <para>内置四级缓存，接近 QQ 的「秒开 + 无闪现」体验：
    ///   1) 内存 Sprite 缓存：命中直接赋值，不再发请求；
    ///   2) 磁盘持久化缓存：首次下载成功后写入本地，下次冷启动直接读本地文件，秒开无闪现；
    ///   3) 下载中集合：同一 URL 并发去重，等首个请求完成后统一赋值；
    ///   4) 失败缓存：失败后短时间内不再重试，避免刷屏超时。</para>
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

        // 本地磁盘头像缓存目录（persistentDataPath/avatars/）
        private static string _cacheDir;

        /// <summary>把服务端返回的相对路径（/uploads/...）拼成绝对地址。</summary>
        public static string ToAbsoluteUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
            var baseUrl = NetManager.Instance != null ? NetManager.Instance.ServerUrl : string.Empty;
            return baseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
        }

        /// <summary>
        /// 加载头像并显示到 image。命中内存/磁盘缓存直接赋值（同步、零延迟）；否则发起网络下载并去重。
        /// <param name="force">强制刷新：忽略缓存并给 URL 追加时间戳绕过 HTTP 缓存（用于头像内容更新但 URL 不变的场景）。</param>
        /// </summary>
        public static void LoadInto(Image image, string url, MonoBehaviour runner, bool force = false)
        {
            if (image == null || string.IsNullOrEmpty(url) || runner == null) return;

            string fullUrl = ToAbsoluteUrl(url);
            if (force)
            {
                Invalidate(url);
                fullUrl = AppendCacheBust(fullUrl);
            }

            // 同步命中：内存 或 磁盘，直接赋值，避免「默认图→真实图」的异步闪烁
            if (TryGetCached(fullUrl, out var cached))
            {
                image.sprite = cached;
                return;
            }

            runner.StartCoroutine(EnsureAvatar(fullUrl, runner, image));
        }

        /// <summary>
        /// 同步把内存/磁盘缓存的头像载入内存（不触发网络下载，不阻塞）。
        /// 用于渲染列表/消息前先确保头像在内存缓存中，避免异步导致的「默认图→真实图」闪烁。
        /// 返回 true 表示已就绪（内存或磁盘命中）；false 表示需走网络下载。
        /// </summary>
        public static bool PreloadIntoMemory(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return TryGetCached(ToAbsoluteUrl(url), out _);
        }

        /// <summary>
        /// 预加载头像到缓存（内存 + 磁盘，不设置任何 Image）。用于好友界面打开时预热。
        /// 命中缓存/下载中则直接返回。
        /// </summary>
        public static void Prefetch(string url, MonoBehaviour runner)
        {
            if (string.IsNullOrEmpty(url) || runner == null) return;

            string fullUrl = ToAbsoluteUrl(url);

            // 失败缓存窗口内，不重试
            if (_failedCache.TryGetValue(fullUrl, out var failTime) &&
                Time.realtimeSinceStartup - failTime < FailCacheSeconds) return;

            // 正在下载中，无需重复预热
            if (_pendingImages.ContainsKey(fullUrl)) return;

            // 内存/磁盘已缓存则载入内存即可，无需网络下载
            if (TryGetCached(fullUrl, out _)) return;

            runner.StartCoroutine(EnsureAvatar(fullUrl, runner, null));
        }

        /// <summary>
        /// 统一头像加载入口：内存 → 磁盘 → 网络。
        /// <param name="target">要赋值的 Image；为 null 表示仅预热（Prefetch）。</param>
        /// </summary>
        private static IEnumerator EnsureAvatar(string fullUrl, MonoBehaviour runner, Image target)
        {
            // 1) 内存缓存：直接赋值
            if (TryGetCached(fullUrl, out var cached))
            {
                if (target != null)
                {
                    target.sprite = cached;
                    FadeIn(target, runner);
                }
                yield break;
            }

            // 2) 网络下载（含去重 + 成功后写磁盘）
            yield return StartNetworkLoad(fullUrl, runner, target);
        }

        /// <summary>
        /// 同步命中头像：先查内存缓存，未命中再同步读磁盘文件并载入内存。
        /// 成功返回 true 并 out sprite。用于列表渲染时零延迟赋值，避免异步闪烁。
        /// </summary>
        private static bool TryGetCached(string fullUrl, out Sprite sprite)
        {
            if (_spriteCache.TryGetValue(fullUrl, out sprite)) return true;

            string localPath = GetLocalPath(fullUrl);
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(localPath);
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                    {
                        sprite = CreateSprite(tex);
                        _spriteCache[fullUrl] = sprite;   // 载入内存，下次秒回
                        return true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AvatarLoader] 读磁盘缓存失败: {e.Message}");
                }
            }

            sprite = null;
            return false;
        }

        /// <summary>
        /// 发起网络下载（带并发去重）。完成后统一赋值给所有等待的 Image，并把图片写入磁盘缓存。
        /// </summary>
        private static IEnumerator StartNetworkLoad(string fullUrl, MonoBehaviour runner, Image target)
        {
            // 去重：同一 URL 正在下载时，仅加入等待列表
            if (_pendingImages.TryGetValue(fullUrl, out var existList))
            {
                if (target != null)
                {
                    if (!existList.Contains(target)) existList.Add(target);
                    FadeOut(target);   // 下载中隐藏，避免「默认图→真实图」闪烁
                }
                yield break;
            }

            var waiters = target != null ? new List<Image> { target } : new List<Image>();
            _pendingImages[fullUrl] = waiters;
            if (target != null) FadeOut(target);

            yield return runner.StartCoroutine(LoadCoroutine(fullUrl, runner));
        }

        /// <summary>下载期间隐藏 Image（透明化，不影响布局），避免占位图到真实头像的突兀替换。</summary>
        private static void FadeOut(Image image)
        {
            if (image == null) return;
            var c = image.color;
            c.a = 0f;
            image.color = c;
        }

        /// <summary>下载完成后平滑淡入显示 Image（约 0.18s），避免「透明 → 突然出现」的生硬感。</summary>
        private static void FadeIn(Image image, MonoBehaviour runner)
        {
            if (image == null || runner == null) return;
            runner.StartCoroutine(FadeInRoutine(image, 0.18f));
        }

        private static IEnumerator FadeInRoutine(Image image, float duration)
        {
            if (image == null) yield break;
            var c = image.color;
            float t = 0f;
            while (t < duration)
            {
                if (image == null) yield break;
                t += Time.deltaTime;
                c.a = Mathf.Clamp01(t / duration);
                image.color = c;
                yield return null;
            }
            if (image != null)
            {
                c.a = 1f;
                image.color = c;
            }
        }

        /// <summary>清空内存缓存（登出/刷新时调用；磁盘缓存保留以便下次秒开）。</summary>
        public static void ClearCache()
        {
            _spriteCache.Clear();
            _pendingImages.Clear();
            _failedCache.Clear();
        }

        /// <summary>清空内存 + 磁盘缓存（登出/隐私场景）。</summary>
        public static void ClearAllCache()
        {
            ClearCache();
            if (_cacheDir != null && Directory.Exists(_cacheDir))
            {
                try { Directory.Delete(_cacheDir, true); }
                catch { /* 忽略删除失败 */ }
                _cacheDir = null;
            }
        }

        /// <summary>
        /// 失效指定 URL 的缓存（内存 + 磁盘），强制下次重新下载。
        /// 用于头像内容更新但 URL 不变（如 /uploads/avatar/{userId}.png）的场景。
        /// </summary>
        public static void Invalidate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            string fullUrl = ToAbsoluteUrl(url);
            _spriteCache.Remove(fullUrl);
            _failedCache.Remove(fullUrl);
            _pendingImages.Remove(fullUrl);
            string localPath = GetLocalPath(fullUrl);
            if (File.Exists(localPath))
            {
                try { File.Delete(localPath); } catch { /* 忽略 */ }
            }
        }

        /// <summary>给 URL 追加时间戳参数，绕过 Unity / nginx 的 HTTP 缓存。</summary>
        private static string AppendCacheBust(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            string sep = url.Contains("?") ? "&" : "?";
            return url + sep + "_t=" + System.DateTime.Now.Ticks;
        }

        /// <summary>本地头像缓存目录，惰性创建。</summary>
        private static string CacheDir
        {
            get
            {
                if (_cacheDir == null)
                {
                    _cacheDir = Path.Combine(Application.persistentDataPath, "avatars");
                    try { Directory.CreateDirectory(_cacheDir); }
                    catch { _cacheDir = null; }
                }
                return _cacheDir;
            }
        }

        /// <summary>URL 对应的本地磁盘路径（按 URL 哈希命名，避免非法字符）。</summary>
        private static string GetLocalPath(string fullUrl)
        {
            var dir = CacheDir;
            if (string.IsNullOrEmpty(dir)) return string.Empty;
            return Path.Combine(dir, ComputeHash(fullUrl) + ".png");
        }

        /// <summary>对字符串做稳定的 MD5 十六进制哈希，用于本地缓存文件名。</summary>
        private static string ComputeHash(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                var sb = new System.Text.StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>由 Texture2D 创建 sprite 的公共步骤。</summary>
        private static Sprite CreateSprite(Texture2D tex)
        {
            return Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private static IEnumerator LoadCoroutine(string fullUrl, MonoBehaviour runner)
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

                var sprite = CreateSprite(tex);

                // 缓存 sprite，后续命中直接复用
                _spriteCache[fullUrl] = sprite;

                // 写磁盘缓存：下次冷启动直接读本地，秒开无闪现。
                // 用 EncodeToPNG 从解码纹理重新编码，比 downloadHandler.data 更可靠，保证 LoadImage 可读。
                try
                {
                    var png = tex.EncodeToPNG();
                    if (png != null && png.Length > 0)
                    {
                        var localPath = GetLocalPath(fullUrl);
                        if (!string.IsNullOrEmpty(localPath))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                            File.WriteAllBytes(localPath, png);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AvatarLoader] 写磁盘缓存失败: {e.Message}");
                }

                // 统一赋值给所有等待的 Image（先恢复可见再设 sprite，避免透明状态残留）
                if (waiters != null)
                {
                    foreach (var img in waiters)
                    {
                        if (img != null)
                        {
                            img.sprite = sprite;
                            FadeIn(img, runner);
                        }
                    }
                }
            }
        }
    }
}
