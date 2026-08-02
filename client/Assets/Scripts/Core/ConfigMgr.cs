using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
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

        public static ConfigMgr Instance { get; } = new ConfigMgr();

        private readonly Dictionary<string, string> _raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>是否至少完成过一次加载。</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>已加载的表数量。</summary>
        public int LoadedCount => _raw.Count;

        public string ConfigRootPath => Path.Combine(Application.streamingAssetsPath, ConfigDir);

        /// <summary>
        /// 同步加载 config 目录下全部 Config_*.json。
        /// 17 张表较小，启动时一次性读入内存；Android 平台需先经 UnityWebRequest 拷贝 StreamingAssets。
        /// </summary>
        public void LoadAll()
        {
            _raw.Clear();
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
