using System.Security.Cryptography;

namespace WildCourt.Api.Utils;

/// <summary>
/// 图形验证码：生成 4 位随机字符（去除易混淆的 0/O/1/I/l），返回 SVG 图片 base64。
/// 无 System.Drawing 依赖，兼容 Linux 容器。
/// </summary>
public class CaptchaService
{
    private const string AllowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";

    public (string Code, string ImageBase64) Generate()
    {
        var code = new string(Enumerable.Range(0, 4)
            .Select(_ => AllowedChars[RandomNumberGenerator.GetInt32(AllowedChars.Length)])
            .ToArray());

        var svg = BuildSvg(code);
        var bytes = System.Text.Encoding.UTF8.GetBytes(svg);
        var base64 = Convert.ToBase64String(bytes);
        return (code, $"data:image/svg+xml;base64,{base64}");
    }

    private static string BuildSvg(string code)
    {
        const int width = 120;
        const int height = 40;
        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#f5f7fa\"/>");

        // 干扰线
        var rng = Random.Shared;
        for (var i = 0; i < 4; i++)
        {
            var x1 = rng.Next(0, width);
            var y1 = rng.Next(0, height);
            var x2 = rng.Next(0, width);
            var y2 = rng.Next(0, height);
            var color = $"#{rng.Next(0x80, 0xFF):X2}{rng.Next(0x80, 0xFF):X2}{rng.Next(0x80, 0xFF):X2}";
            sb.Append($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{color}\" stroke-width=\"1\" opacity=\"0.6\"/>");
        }

        // 字符（随机角度位移）
        for (var i = 0; i < code.Length; i++)
        {
            var x = 12 + i * 25;
            var y = 26 + rng.Next(-4, 5);
            var rotate = rng.Next(-20, 21);
            var color = $"#{rng.Next(0x20, 0x90):X2}{rng.Next(0x20, 0x90):X2}{rng.Next(0x20, 0x90):X2}";
            sb.Append($"<text x=\"{x}\" y=\"{y}\" font-size=\"24\" font-family=\"monospace\" font-weight=\"bold\" fill=\"{color}\" transform=\"rotate({rotate} {x} {y})\">{code[i]}</text>");
        }

        // 噪点
        for (var i = 0; i < 40; i++)
        {
            var x = rng.Next(0, width);
            var y = rng.Next(0, height);
            sb.Append($"<circle cx=\"{x}\" cy=\"{y}\" r=\"1\" fill=\"#999\"/>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}
