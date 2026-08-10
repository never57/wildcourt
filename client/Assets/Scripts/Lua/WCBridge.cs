using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using WildCourt.Core;
using WildCourt.Net;
using WildCourt.Net.Protocol;

#if USE_XLUA
using XLua;
#endif

namespace WildCourt.Lua
{
    /// <summary>
    /// WC.* 桥接（文档 lua-api.md §1~§5.5）：把 C# 的配置 / 网络 / 计时能力挂到 Lua 全局表 WC。
    /// 仅在 USE_XLUA 宏开启时生效（<see cref="LuaManager.Init"/> 调用 Bind）；未开启时保持桩模式。
    /// 回调统一 resp = { code, data, msg }，与 lua-api.md 约定一致。
    /// </summary>
    public static class WCBridge
    {
#if USE_XLUA
        /// <summary>把 WC 表挂进 LuaEnv.Global，并确保计时器服务存在。</summary>
        public static void Bind(LuaEnv env)
        {
            EnsureTimerService();
            var wc = env.NewTable();
            wc.Set("config", BuildConfig(env));
            wc.Set("net", BuildNet(env));
            wc.Set("timer", BuildTimer(env));
            env.Global.Set("WC", wc);
        }

        // ---------- WC.config ----------

        private static LuaTable BuildConfig(LuaEnv env)
        {
            Func<string, LuaTable> getTable = name =>
            {
                string json = ConfigMgr.Instance.GetRawJson(name);
                if (string.IsNullOrEmpty(json)) return null;
                var root = ParseJson(env, json) as LuaTable;
                return root == null ? null : root.Get<LuaTable>("rows");
            };

            var t = env.NewTable();
            t.Set("getTable", getTable);
            t.Set("getRowById", new Func<string, object, LuaTable>((name, id) =>
            {
                var rows = getTable(name);
                if (rows == null) return null;
                string want = Convert.ToString(id, CultureInfo.InvariantCulture);
                LuaTable found = null;
                rows.ForEach<object, object>((k, v) =>
                {
                    if (found != null) return;
                    if (v is LuaTable row)
                    {
                        var rowId = row.Get<object>("id");
                        if (rowId != null && Convert.ToString(rowId, CultureInfo.InvariantCulture) == want)
                        {
                            found = row;
                        }
                    }
                });
                return found;
            }));
            return t;
        }

        // ---------- WC.net ----------

        private static LuaTable BuildNet(LuaEnv env)
        {
            var t = env.NewTable();

            t.Set("get", new Action<string, LuaTable, LuaFunction>((path, query, cb) =>
            {
                string full = query == null ? path : AppendQuery(path, query);
                RequestRaw(env, "GET", full, null, cb);
            }));
            t.Set("post", new Action<string, LuaTable, LuaFunction>((path, body, cb) =>
            {
                RequestRaw(env, "POST", path, body == null ? null : LuaTableToJson(body), cb);
            }));
            t.Set("put", new Action<string, LuaTable, LuaFunction>((path, body, cb) =>
            {
                RequestRaw(env, "PUT", path, body == null ? null : LuaTableToJson(body), cb);
            }));

            // WC.net.API：反射 ApiPaths 静态常量（const string）
            var api = env.NewTable();
            foreach (var f in typeof(ApiPaths).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType == typeof(string)) api.Set(f.Name, (string)f.GetValue(null));
            }
            t.Set("API", api);

            // WC.net.CODE：反射 ApiCodes 静态常量（const int）
            var code = env.NewTable();
            foreach (var f in typeof(ApiCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType == typeof(int)) code.Set(f.Name, (int)f.GetValue(null));
            }
            t.Set("CODE", code);

            return t;
        }

        /// <summary>发原始请求并把信封文本解析为 { code, data, msg } 回调给 Lua。</summary>
        private static void RequestRaw(LuaEnv env, string method, string path, string body, LuaFunction cb)
        {
            if (cb == null) return;
            var net = NetManager.Instance;
            if (net == null)
            {
                InvokeLuaError(env, cb, ApiCodes.NetworkError, "NetManager 未初始化");
                return;
            }

            Action<string> onSuccess = text =>
            {
                var resp = env.NewTable();
                resp.Set("code", 0);
                var parsed = ParseJson(env, text);
                if (parsed is LuaTable obj)
                {
                    var codeVal = obj.Get<object>("code");
                    if (codeVal != null) resp.Set("code", Convert.ToInt64(codeVal));
                    resp.Set("data", obj.Get<object>("data"));
                    var msg = obj.Get<object>("message");
                    if (msg == null) msg = obj.Get<object>("msg");
                    resp.Set("msg", msg == null ? "" : Convert.ToString(msg, CultureInfo.InvariantCulture));
                }
                else
                {
                    resp.Set("data", parsed);
                    resp.Set("msg", "");
                }
                cb.Call(resp);
            };
            Action<int, string> onError = (code, msg) => InvokeLuaError(env, cb, code, msg);

            switch (method)
            {
                case "GET": net.GetRaw(path, onSuccess, onError); break;
                case "POST": net.PostRaw(path, body, onSuccess, onError); break;
                case "PUT": net.PutRaw(path, body, onSuccess, onError); break;
            }
        }

        private static void InvokeLuaError(LuaEnv env, LuaFunction cb, int code, string msg)
        {
            var resp = env.NewTable();
            resp.Set("code", code);
            resp.Set("data", (object)null);
            resp.Set("msg", msg ?? "");
            cb.Call(resp);
        }

        private static string AppendQuery(string path, LuaTable query)
        {
            var sb = new StringBuilder(path);
            bool first = true;
            query.ForEach<object, object>((k, v) =>
            {
                sb.Append(first ? '?' : '&');
                first = false;
                sb.Append(Uri.EscapeDataString(Convert.ToString(k, CultureInfo.InvariantCulture)))
                  .Append('=')
                  .Append(Uri.EscapeDataString(v == null ? string.Empty : Convert.ToString(v, CultureInfo.InvariantCulture)));
            });
            return sb.ToString();
        }

        // ---------- WC.timer ----------

        private static LuaTable BuildTimer(LuaEnv env)
        {
            var t = env.NewTable();
            t.Set("setTimeout", new Func<LuaFunction, double, long>(
                (cb, ms) => LuaTimerService.Instance.SetTimeout(cb, ms)));
            t.Set("clearTimeout", new Action<long>(h => LuaTimerService.Instance.ClearTimeout(h)));
            return t;
        }

        private static void EnsureTimerService()
        {
            if (LuaTimerService.Instance != null) return;
            var go = new GameObject("LuaTimerService");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<LuaTimerService>();
        }

        // ---------- JSON 转换（不引入第三方库，覆盖 Lua 侧请求/响应场景） ----------

        private static object ParseJson(LuaEnv env, string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return new JsonParser(env, json).ParseValue();
        }

        private static string LuaTableToJson(LuaTable t)
        {
            var sb = new StringBuilder();
            WriteValue(sb, t);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case LuaTable table: WriteTable(sb, table); break;
                default: WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture)); break;
            }
        }

        private static void WriteTable(StringBuilder sb, LuaTable t)
        {
            var entries = new List<KeyValuePair<object, object>>();
            bool isArray = true;
            int maxIdx = 0;
            t.ForEach<object, object>((k, v) =>
            {
                entries.Add(new KeyValuePair<object, object>(k, v));
                if (k is double d && d > 0 && d == Math.Floor(d) && d <= int.MaxValue)
                {
                    int idx = (int)d;
                    if (idx > maxIdx) maxIdx = idx;
                }
                else
                {
                    isArray = false;
                }
            });

            if (isArray && entries.Count > 0)
            {
                var arr = new object[maxIdx];
                foreach (var kv in entries) arr[(int)(double)kv.Key - 1] = kv.Value;
                sb.Append('[');
                for (int i = 0; i < arr.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteValue(sb, arr[i]);
                }
                sb.Append(']');
                return;
            }

            sb.Append('{');
            bool first = true;
            foreach (var kv in entries)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, Convert.ToString(kv.Key, CultureInfo.InvariantCulture));
                sb.Append(':');
                WriteValue(sb, kv.Value);
            }
            sb.Append('}');
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
        }

        /// <summary>极简 JSON 解析器：对象 → LuaTable（字符串键）、数组 → LuaTable（1 起始数字键）、数字 → double。</summary>
        private sealed class JsonParser
        {
            private readonly LuaEnv _env;
            private readonly string _s;
            private int _i;

            public JsonParser(LuaEnv env, string s)
            {
                _env = env;
                _s = s;
            }

            public object ParseValue()
            {
                SkipWs();
                if (_i >= _s.Length) return null;
                switch (_s[_i])
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ParseNumber();
                }
            }

            private LuaTable ParseObject()
            {
                _i++; // '{'
                var t = _env.NewTable();
                SkipWs();
                if (Peek() == '}') { _i++; return t; }
                while (true)
                {
                    SkipWs();
                    string key = ParseString();
                    SkipWs();
                    Expect(":");
                    object val = ParseValue();
                    t.Set(key, val);
                    SkipWs();
                    char c = _s[_i];
                    if (c == ',') { _i++; continue; }
                    if (c == '}') { _i++; return t; }
                    throw new FormatException("JSON 对象缺少 , 或 }");
                }
            }

            private LuaTable ParseArray()
            {
                _i++; // '['
                var t = _env.NewTable();
                int idx = 1;
                SkipWs();
                if (Peek() == ']') { _i++; return t; }
                while (true)
                {
                    object val = ParseValue();
                    t.Set(idx, val);
                    idx++;
                    SkipWs();
                    char c = _s[_i];
                    if (c == ',') { _i++; continue; }
                    if (c == ']') { _i++; return t; }
                    throw new FormatException("JSON 数组缺少 , 或 ]");
                }
            }

            private string ParseString()
            {
                _i++; // '"'
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw new FormatException("JSON 字符串未闭合");
                    char c = _s[_i];
                    if (c == '"') { _i++; return sb.ToString(); }
                    if (c == '\\')
                    {
                        _i++;
                        char e = _s[_i];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                sb.Append((char)Convert.ToInt32(_s.Substring(_i + 1, 4), 16));
                                _i += 4;
                                break;
                            default: throw new FormatException("非法转义: \\" + e);
                        }
                        _i++;
                    }
                    else
                    {
                        sb.Append(c);
                        _i++;
                    }
                }
            }

            private object ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length && ("+-0123456789.eE".IndexOf(_s[_i]) >= 0)) _i++;
                string num = _s.Substring(start, _i - start);
                if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                throw new FormatException("非法数字: " + num);
            }

            private void SkipWs()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            private char Peek() => _i < _s.Length ? _s[_i] : '\0';

            private void Expect(string lit)
            {
                if (_i + lit.Length > _s.Length || _s.Substring(_i, lit.Length) != lit)
                {
                    throw new FormatException("期望 " + lit);
                }
                _i += lit.Length;
            }
        }
#endif
    }
}
