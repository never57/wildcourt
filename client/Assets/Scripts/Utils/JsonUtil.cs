using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildCourt.Utils
{
    /// <summary>
    /// JSON 工具：基于 Unity JsonUtility 的封装。
    /// Unity JsonUtility 不支持顶层数组，约定所有数组型数据包一层
    /// <c>{ "rows": [ ... ] }</c>（与 Config_*.json 及服务端信封对齐）。
    /// </summary>
    public static class JsonUtil
    {
        [Serializable]
        private class ArrayWrapper<T>
        {
            public T[] rows;
        }

        /// <summary>解析 <c>{ "rows": [...] }</c> 形式的数组 JSON。</summary>
        public static T[] FromJsonArray<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<T>();
            var wrapper = JsonUtility.FromJson<ArrayWrapper<T>>(json);
            return wrapper == null || wrapper.rows == null ? Array.Empty<T>() : wrapper.rows;
        }

        /// <summary>解析单对象。</summary>
        public static T FromJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            return JsonUtility.FromJson<T>(json);
        }

        /// <summary>序列化（pretty 可选）。</summary>
        public static string ToJson(object obj, bool pretty = false)
        {
            return JsonUtility.ToJson(obj, pretty);
        }

        /// <summary>字段名大小写不敏感地取 JSON 文本中的字段（供 Lua 侧/日志使用）。</summary>
        public static string GetField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int idx = json.IndexOf(fieldName, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + fieldName.Length);
            if (colon < 0) return null;
            int start = colon + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
            if (start >= json.Length) return null;
            char quote = json[start];
            if (quote == '"')
            {
                int end = json.IndexOf('"', start + 1);
                return end < 0 ? null : json.Substring(start + 1, end - start - 1);
            }
            int end2 = start;
            while (end2 < json.Length && (char.IsLetterOrDigit(json[end2]) || json[end2] == '-' || json[end2] == '.')) end2++;
            return json.Substring(start, end2 - start);
        }

        /// <summary>把简单字典编码为 JSON 对象（Lua → C# 传参辅助）。</summary>
        public static string EncodeObject(IReadOnlyDictionary<string, object> fields)
        {
            var sb = new System.Text.StringBuilder("{");
            bool first = true;
            foreach (var kv in fields)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key).Append("\":");
                switch (kv.Value)
                {
                    case string s: sb.Append('"').Append(s.Replace("\"", "\\\"")).Append('"'); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case int i: sb.Append(i); break;
                    case long l: sb.Append(l); break;
                    case float f: sb.Append(f.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
                    default: sb.Append('"').Append(kv.Value).Append('"'); break;
                }
            }
            sb.Append('}');
            return sb.ToString();
        }
    }
}
