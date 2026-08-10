using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WildCourt.Api.Utils;

/// <summary>
/// 运营后台令牌：自实现 HMAC-SHA256 签名 Token（payload.base64url + 签名），零第三方依赖。
/// 与游戏用户会话（Redis）完全隔离，仅用于管理后台鉴权。
/// </summary>
public class AdminTokenService
{
    private readonly byte[] _key;
    private readonly TimeSpan _ttl;

    public AdminTokenService(string secret, TimeSpan ttl)
    {
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        _ttl = ttl;
    }

    public string Issue(long adminId)
    {
        var payload = new Dictionary<string, object>
        {
            ["sub"] = adminId,
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["exp"] = DateTimeOffset.UtcNow.Add(_ttl).ToUnixTimeSeconds(),
        };
        var body = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var sig = Sign(body);
        return $"{body}.{sig}";
    }

    public long? TryValidate(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 2) return null;
        if (!FixedTimeEquals(parts[1], Sign(parts[0]))) return null;
        try
        {
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
            var root = doc.RootElement;
            if (!root.TryGetProperty("exp", out var expProp) || !root.TryGetProperty("sub", out var subProp)) return null;
            if (expProp.GetInt64() < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
            return subProp.GetInt64();
        }
        catch
        {
            return null;
        }
    }

    private string Sign(string body)
    {
        using var hmac = new HMACSHA256(_key);
        return Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
