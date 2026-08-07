using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WildCourt.Utils
{
    /// <summary>
    /// MD5 工具（文档 14 §14.3）：热更时逐文件 MD5 对比本地与 manifest.json，
    /// 校验失败删除重下（文档 14 §14.4）。
    /// </summary>
    public static class MD5Util
    {
        /// <summary>计算文件 MD5（小写十六进制）。文件不存在返回 null。</summary>
        public static string FileMD5(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] hash = md5.ComputeHash(stream);
                return ToHex(hash);
            }
        }

        /// <summary>计算字符串 MD5（小写十六进制，用于协议/校验）。</summary>
        public static string StringMD5(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
                return ToHex(hash);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
