using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using WildCourt.Utils;

namespace WildCourt.Core
{
    /// <summary>
    /// 配置管理器（文档 03 §3.1/§3.5、16）：只读加载 StreamingAssets/config/ 下
    /// Excel 导出的 Config_*.json 数值表，供 C# 与 Lua 查询；热更后调用 <see cref="Reload"/> 重读。
    /// <para>JSON 结构约定（与 Excel2JSON.py 对齐）：<c>{ "rows": [ { ... }, ... ] }</c></para>
    /// </summary>
    public sealed class ConfigMgr
    {
        public const string ConfigDir = "config";
        public const string FilePrefix = "Config_";
        /// <summary>Android 平台无法列出 StreamingAssets 目录，需借助表名清单逐表读取。</summary>
        public const string ManifestFile = "manifest.txt";

        public static ConfigMgr Instance { get; } = new ConfigMgr();

        private readonly Dictionary<string, string> _raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>是否至少完成过一次加载。</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>已加载的表数量。</summary>
        public int LoadedCount => _raw.Count;

        public string ConfigRootPath => Path.Combine(Application.streamingAssetsPath, ConfigDir);

        /// <summary>
        /// 同步加载 config 目录下全部 Config_*.json。
        /// Android 上 StreamingAssets 位于 APK 内（jar:file:// URL），Directory/File API 不可用，
        /// 故改用 UnityWebRequest 读取（表很小，同步等待可接受），表名清单见 config/manifest.txt。
        /// 其余平台保持文件系统扫描。
        /// </summary>
        public void LoadAll()
        {
            _raw.Clear();

            if (Application.platform == RuntimePlatform.Android)
            {
                LoadAllFromStreamingAssets();
                return;
            }

            string dir = ConfigRootPath;
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[ConfigMgr] 配置目录不存在: {dir}");
                IsLoaded = false;
                return;
            }

            foreach (string file in Directory.GetFiles(dir, FilePrefix + "*.json"))
            {
                string tableName = Path.GetFileNameWithoutExtension(file).Substring(FilePrefix.Length);
                _raw[tableName] = File.ReadAllText(file);
            }

            IsLoaded = true;
            Debug.Log($"[ConfigMgr] 已加载 {_raw.Count} 张配置表: {string.Join(", ", _raw.Keys)}");
        }

        /// <summary>
        /// Android：StreamingAssets 是 jar:file:// URL，且无法列出目录内文件，
        /// 故按 manifest.txt 中的表名清单逐个用 UnityWebRequest 读取。
        /// </summary>
        private void LoadAllFromStreamingAssets()
        {
            string manifestUrl = StreamingAssetsFileUrl(Path.Combine(ConfigDir, ManifestFile));
            string[] tableNames = ReadTextLines(manifestUrl);
            if (tableNames.Length == 0)
            {
                Debug.LogWarning($"[ConfigMgr] manifest 为空或读取失败: {manifestUrl}");
                IsLoaded = false;
                return;
            }

            foreach (string name in tableNames)
            {
                string url = StreamingAssetsFileUrl(Path.Combine(ConfigDir, FilePrefix + name + ".json"));
                string json = ReadText(url);
                if (json != null)
                {
                    _raw[name] = json;
                }
                else
                {
                    Debug.LogWarning($"[ConfigMgr] 读取配置表失败: {name} ({url})");
                }
            }

            IsLoaded = _raw.Count > 0;
            Debug.Log($"[ConfigMgr] Android 已加载 {_raw.Count} 张配置表: {string.Join(", ", _raw.Keys)}");
        }

        /// <summary>构造 StreamingAssets 内文件的 URL（Android 已带 jar:file:// 前缀，其余平台补 file://）。</summary>
        private static string StreamingAssetsFileUrl(string relativePath)
        {
            string path = Path.Combine(Application.streamingAssetsPath, relativePath);
            return Application.platform == RuntimePlatform.Android
                ? path
                : "file://" + path.Replace('\\', '/');
        }

        private static string[] ReadTextLines(string url)
        {
            string text = ReadText(url);
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
            return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string ReadText(string url)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SendWebRequest();
                // 同步等待完成（部分 Unity 2022.3 构建不支持 WaitForCompletion，用自旋兼容）
                while (!request.isDone)
                {
                    // 等待 UWR 完成
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return request.downloadHandler?.text;
                }

                Debug.LogWarning($"[ConfigMgr] 读取失败: {url} :: {request.error}");
                return null;
            }
        }

        /// <summary>热更完成后重读全部配置（文档 14 §14.3）。</summary>
        public void Reload()
        {
            LoadAll();
            EventBus.Publish(new ConfigReloadedEvent());
        }

        public bool HasTable(string tableName) => _raw.ContainsKey(tableName);

        public string GetRawJson(string tableName)
        {
            return _raw.TryGetValue(tableName, out var json) ? json : null;
        }

        /// <summary>取整张表（tableName 不含 Config_ 前缀，如 "PlayerLevel"）。</summary>
        public T[] GetTable<T>(string tableName)
        {
            string json = GetRawJson(tableName);
            if (string.IsNullOrEmpty(json)) return Array.Empty<T>();
            return JsonUtil.FromJsonArray<T>(json);
        }

        /// <summary>按谓词取单行。</summary>
        public T GetRow<T>(string tableName, Func<T, bool> predicate)
        {
            foreach (T row in GetTable<T>(tableName))
            {
                if (predicate(row)) return row;
            }
            return default;
        }
    }
}
