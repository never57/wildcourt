using System.Security.Cryptography;
using System.Text;

namespace WildCourt.Api.Utils;

/// <summary>
/// TOTP（RFC 6238）双因素认证：基于 HMAC-SHA1 + Base32 密钥，30 秒窗口、6 位动态码。
/// 无第三方依赖。
/// </summary>
public class TotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const int KeyLengthBytes = 20; // 160 bit

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>生成新的 Base32 密钥（不含填充 =）。</summary>
    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(KeyLengthBytes);
        return Base32Encode(bytes);
    }

    /// <summary>生成 otpauth:// URI（供扫码器/认证器导入）。</summary>
    public string BuildOtpUri(string secret, string account, string issuer = "WildCourt Admin")
    {
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
               $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
    }

    /// <summary>校验一次性动态码（允许前后各 1 个时间窗的时钟漂移）。</summary>
    public bool Validate(string secret, string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits || !code.All(char.IsDigit))
        {
            return false;
        }
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
        for (var offset = -1; offset <= 1; offset++)
        {
            if (TimingSafeEqual(Compute(counter + offset, secret), code))
            {
                return true;
            }
        }
        return false;
    }

    private static string Compute(long counter, string secret)
    {
        var key = Base32Decode(secret);
        var timeBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(timeBytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                   | ((hash[offset + 1] & 0xFF) << 16)
                   | ((hash[offset + 2] & 0xFF) << 8)
                   | (hash[offset + 3] & 0xFF);
        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString(new string('0', Digits));
    }

    private static bool TimingSafeEqual(string expected, string actual)
    {
        if (expected.Length != actual.Length) return false;
        var result = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            result |= expected[i] ^ actual[i];
        }
        return result == 0;
    }

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder();
        int buffer = 0, bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                sb.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 0x1F]);
                bitsLeft -= 5;
            }
        }
        if (bitsLeft > 0)
        {
            sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }
        return sb.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        var clean = input.Trim().TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bitsLeft = 0;
        foreach (var c in clean)
        {
            var value = Base32Alphabet.IndexOf(c);
            if (value < 0) continue;
            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }
        return output.ToArray();
    }
}
