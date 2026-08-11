using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WildCourt.Mcp;

/// <summary>
/// 管理后台 API 客户端：负责登录（密码 SHA256 hex）→ 缓存 JWT → 自动续期 → 带令牌调用。
/// 响应信封约定：{ Code, Data, Message }，Code==0 表示成功。
/// </summary>
public class AdminApiClient
{
    private readonly HttpClient _http = new();
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _passwordSha256;
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    private string? _token;
    private DateTime _tokenExpireAtUtc = DateTime.MinValue;

    public AdminApiClient(string baseUrl, string username, string password)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _username = username;
        _passwordSha256 = Sha256Hex(password);
    }

    public string BaseUrl => _baseUrl;

    /// <summary>确保已登录：无令牌或临近过期时自动登录。</summary>
    private async Task<string> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow < _tokenExpireAtUtc.AddMinutes(-1))
        {
            return _token;
        }

        await _loginLock.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow < _tokenExpireAtUtc.AddMinutes(-1))
            {
                return _token;
            }

            var response = await _http.PostAsJsonAsync($"{_baseUrl}/auth/login", new
            {
                Username = _username,
                Password = _passwordSha256,
            });

            var envelope = await ReadEnvelopeAsync(response);
            if (envelope.Data.ValueKind == JsonValueKind.Undefined)
            {
                throw new McpApiException($"登录失败: {envelope.Message}");
            }

            using var doc = JsonDocument.Parse(envelope.Data.GetRawText());
            var root = doc.RootElement;
            _token = root.TryGetProperty("token", out var t) ? t.GetString() : string.Empty;
            var expireAt = root.TryGetProperty("expireAt", out var e) && e.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(e.GetString(), out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow.AddHours(1)
                : DateTime.UtcNow.AddHours(1);

            if (string.IsNullOrEmpty(_token))
            {
                throw new McpApiException("登录成功但未返回令牌");
            }

            _tokenExpireAtUtc = expireAt;
            return _token;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    /// <summary>GET 请求，返回 Data 的 JSON 文本（未包装）。</summary>
    public async Task<string> GetAsync(string path, Dictionary<string, string?>? query = null)
    {
        var url = BuildUrl(path, query);
        var response = await SendWithAuthAsync(HttpMethod.Get, url, null);
        return await ReadDataJsonAsync(response);
    }

    /// <summary>POST JSON 请求。</summary>
    public async Task<string> PostAsync(string path, object? body = null)
    {
        var url = BuildUrl(path, null);
        var response = await SendWithAuthAsync(HttpMethod.Post, url, body);
        return await ReadDataJsonAsync(response);
    }

    /// <summary>PUT JSON 请求。</summary>
    public async Task<string> PutAsync(string path, object? body = null)
    {
        var url = BuildUrl(path, null);
        var response = await SendWithAuthAsync(HttpMethod.Put, url, body);
        return await ReadDataJsonAsync(response);
    }

    /// <summary>DELETE 请求（可带查询参数或 JSON body）。</summary>
    public async Task<string> DeleteAsync(string path, Dictionary<string, string?>? query = null, object? body = null)
    {
        var url = BuildUrl(path, query);
        var response = await SendWithAuthAsync(HttpMethod.Delete, url, body);
        return await ReadDataJsonAsync(response);
    }

    private async Task<HttpResponseMessage> SendWithAuthAsync(HttpMethod method, string url, object? body)
    {
        var token = await GetTokenAsync();

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await _http.SendAsync(request);

        // 令牌失效（信封 code=401）→ 强制重新登录后重试一次
        if (response.IsSuccessStatusCode)
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (raw.Contains("\"code\":401", StringComparison.OrdinalIgnoreCase))
            {
                await ForceReLoginAsync();
                using var retry = new HttpRequestMessage(method, url);
                retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync());
                if (body is not null)
                {
                    retry.Content = JsonContent.Create(body);
                }
                return await _http.SendAsync(retry);
            }
        }

        return response;
    }

    private async Task ForceReLoginAsync()
    {
        await _loginLock.WaitAsync();
        try
        {
            _token = null;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task<string> ReadDataJsonAsync(HttpResponseMessage response)
    {
        var envelope = await ReadEnvelopeAsync(response);
        if (envelope.Data.ValueKind == JsonValueKind.Undefined)
        {
            throw new McpApiException(envelope.Message);
        }
        return envelope.Data.GetRawText();
    }

    private static async Task<ApiEnvelope> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<ApiEnvelope>(raw, JsonOpts.Default);
        if (envelope is null)
        {
            throw new McpApiException($"响应解析失败 (HTTP {(int)response.StatusCode}): {Truncate(raw)}");
        }
        if (envelope.Code != 0)
        {
            throw new McpApiException($"API 错误[{envelope.Code}]: {envelope.Message}");
        }
        return envelope;
    }

    private string BuildUrl(string path, Dictionary<string, string?>? query)
    {
        var sb = new StringBuilder(_baseUrl);
        if (!path.StartsWith('/'))
        {
            sb.Append('/');
        }
        sb.Append(path);
        if (query is { Count: > 0 })
        {
            sb.Append('?');
            var first = true;
            foreach (var (key, value) in query)
            {
                if (!first)
                {
                    sb.Append('&');
                }
                first = false;
                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(value ?? string.Empty));
            }
        }
        return sb.ToString();
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Truncate(string s, int max = 500)
    {
        return s.Length <= max ? s : s[..max] + "...(截断)";
    }
}

/// <summary>统一响应信封。</summary>
public class ApiEnvelope
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public JsonElement Data { get; set; }
}

/// <summary>API 调用异常，消息会直接透传给调用方。</summary>
public class McpApiException : Exception
{
    public McpApiException(string message) : base(message) { }
}

/// <summary>JSON 序列化选项。</summary>
public static class JsonOpts
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
