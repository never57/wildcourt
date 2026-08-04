using System.Text.Json;
using WlidCourt.Api.Models;

namespace WlidCourt.Api.Services;

/// <summary>
/// 数值配置加载（文档 16 §16.3）：服务端与客户端同读同一份 JSON 配置（Config_*.json）。
/// 目录由 Program.cs 解析（WLIDCOURT_CONFIG_DIR 或 ContentRoot ../../config），
/// 文件结构统一 { "rows": [ ... ] }（与客户端 ConfigMgr / Excel2JSON.py 对齐）。
/// 简单缓存：进程内只读，热更后重启生效。
/// </summary>
public class ConfigService
{
    private readonly string _configRoot;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ConfigService(string configRoot)
    {
        _configRoot = configRoot;
    }

    /// <summary>读取一张配置表的行（fileName 含 .json 后缀，如 "Config_YardUnlock.json"）。</summary>
    public T[] GetRows<T>(string fileName)
    {
        string json = Load(fileName);
        var wrapper = JsonSerializer.Deserialize<RowsWrapper<T>>(json, JsonOpts);
        return wrapper?.Rows ?? Array.Empty<T>();
    }

    private string Load(string fileName)
    {
        if (_cache.TryGetValue(fileName, out var cached)) return cached;

        string path = Path.Combine(_configRoot, fileName);
        if (!File.Exists(path))
        {
            throw new BusinessException(500, $"配置表缺失: {fileName}");
        }
        string json = File.ReadAllText(path);
        _cache[fileName] = json;
        return json;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>JSON 行包装：{ "rows": [...] }（与客户端 ConfigMgr / Excel2JSON.py 对齐）。</summary>
    public sealed class RowsWrapper<T>
    {
        public T[]? Rows { get; set; }
    }
}
