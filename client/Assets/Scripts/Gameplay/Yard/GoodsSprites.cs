using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace WildCourt.Gameplay.Yard
{
    /// <summary>
    /// 商品图异步加载（热更美术，文档 14）：StreamingAssets/ui/Decorations/{id}.png。
    /// 有图用图、无图回退分类色块；加载完成回调，调用方负责刷新 Image/SpriteRenderer。
    /// <para>
    /// 并发去重：同一 id 的多个请求共享一次下载，所有回调在加载完成后统一触发。
    /// 这样商店格子重建（翻页 / 切分类 / 重开面板）时，新格子也能收到图片，避免"图片不刷新"。
    /// </para>
    /// </summary>
    public static class GoodsSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new();
        private static readonly Dictionary<string, List<Action<string, Sprite>>> Pending = new();
        private static readonly HashSet<string> Missed = new();
        private static SpriteRunner _runner;

        /// <summary>
        /// 请求加载商品图：
        /// - 命中缓存：立即回调；
        /// - 已知失败/无图：立即回调 null；
        /// - 加载中：登记回调，加载完成后统一触发；
        /// - 未加载：启动下载并登记回调。
        /// </summary>
        public static void Request(string id, Action<string, Sprite> onLoaded)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (onLoaded == null) return;

            // 1. 命中缓存：立即回调
            if (Cache.TryGetValue(id, out var sprite))
            {
                onLoaded(id, sprite);
                return;
            }

            // 2. 已知失败/无图：立即回调 null
            if (Missed.Contains(id))
            {
                onLoaded(id, null);
                return;
            }

            // 3. 已在加载中：登记回调，等待统一触发（关键修复点）
            if (Pending.TryGetValue(id, out var waiters))
            {
                waiters.Add(onLoaded);
                return;
            }

            // 4. 首次加载：登记回调 + 启动下载
            Pending[id] = new List<Action<string, Sprite>> { onLoaded };
            EnsureRunner();
            _runner.StartCoroutine(Load(id));
        }

        private static IEnumerator Load(string id)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "ui", "Decorations", id + ".png");
            // Android 的 streamingAssetsPath 已是 jar:file:// 前缀，直接使用；其余平台补 file://
            string url = Application.platform == RuntimePlatform.Android
                ? path
                : "file://" + path.Replace('\\', '/');

            Sprite sprite = null;
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var tex = DownloadHandlerTexture.GetContent(request);
                    if (tex != null)
                    {
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        Cache[id] = sprite;
                    }
                    else
                    {
                        Missed.Add(id);
                    }
                }
                else
                {
                    Missed.Add(id);
                    Debug.LogWarning($"[GoodsSprites] 加载失败 {id}: {request.error}");
                }
            }

            // 取出并清空等待队列，逐个触发回调（加载期间新增的请求也已登记）
            if (Pending.TryGetValue(id, out var waiters))
            {
                Pending.Remove(id);
                for (int i = 0; i < waiters.Count; i++)
                {
                    var cb = waiters[i];
                    if (cb == null) continue;
                    try { cb(id, sprite); }
                    catch (Exception e) { Debug.LogError($"[GoodsSprites] 回调异常 {id}: {e}"); }
                }
            }
        }

        /// <summary>清空缓存（重载资源/热更后调用）。</summary>
        public static void Clear()
        {
            Cache.Clear();
            Pending.Clear();
            Missed.Clear();
        }

        private static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("GoodsSpriteRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<SpriteRunner>();
        }

        /// <summary>仅用于承载加载协程。</summary>
        private sealed class SpriteRunner : MonoBehaviour
        {
        }
    }
}
