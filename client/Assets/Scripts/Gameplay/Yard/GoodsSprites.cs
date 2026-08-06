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
    /// </summary>
    public static class GoodsSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new();
        private static readonly HashSet<string> InFlight = new();
        private static readonly HashSet<string> Missed = new();
        private static SpriteRunner _runner;

        /// <summary>请求加载商品图：命中缓存立即回调；失败/无图回调 null；加载中防重。</summary>
        public static void Request(string id, Action<string, Sprite> onLoaded)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Cache.TryGetValue(id, out var sprite))
            {
                onLoaded?.Invoke(id, sprite);
                return;
            }
            if (Missed.Contains(id))
            {
                onLoaded?.Invoke(id, null);
                return;
            }
            if (!InFlight.Add(id))
            {
                return; // 已在加载中，首个请求回调即可
            }

            EnsureRunner();
            _runner.StartCoroutine(Load(id, onLoaded));
        }

        private static IEnumerator Load(string id, Action<string, Sprite> onLoaded)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "ui", "Decorations", id + ".png");
            // Android 的 streamingAssetsPath 已是 jar:file:// 前缀，直接使用；其余平台补 file://
            string url = Application.platform == RuntimePlatform.Android
                ? path
                : "file://" + path.Replace('\\', '/');

            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();

                Sprite sprite = null;
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

                InFlight.Remove(id);
                onLoaded?.Invoke(id, sprite);
            }
        }

        /// <summary>清空缓存（重载资源/热更后调用）。</summary>
        public static void Clear()
        {
            Cache.Clear();
            InFlight.Clear();
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
