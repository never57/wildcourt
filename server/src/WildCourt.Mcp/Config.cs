using System.Text.Json;

namespace WildCourt.Mcp;

/// <summary>极简配置读取：appsettings.json（开发默认值）→ 环境变量（生产覆盖）。</summary>
public static class Config
{
    private static readonly Dictionary<string, string?> _values = Load();

    private static Dictionary<string, string?> Load()
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        // 先加载默认配置，再用本地配置覆盖（appsettings.local.json 不入库，用于存放真实密码）
        foreach (var name in new[] { "appsettings.json", "appsettings.local.json" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, name);
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                Flatten(doc.RootElement, string.Empty, result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WildCourt.Mcp] {name} 解析失败: {ex.Message}");
            }
        }
        return result;
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string?> target)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}:{prop.Name}";
                Flatten(prop.Value, key, target);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            target[prefix] = element.GetString();
        }
        else
        {
            target[prefix] = element.GetRawText();
        }
    }

    /// <summary>读取配置项（支持 "Admin:BaseUrl" 形式），不存在返回 null。</summary>
    public static string? Get(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : null;
    }
}
