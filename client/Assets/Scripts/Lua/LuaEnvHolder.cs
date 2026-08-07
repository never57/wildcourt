using System.IO;
using UnityEngine;

#if USE_XLUA
using XLua;
#endif

namespace WildCourt.Lua
{
    /// <summary>
    /// XLua 全局环境持有者（文档 03 §3.4）。
    /// <para>需在 Player Settings → Scripting Define Symbols 添加 <c>USE_XLUA</c> 并安装 XLua 插件
    /// （Assets/XLua/，见 client/README.md）后启用；未启用时框架以 C# 桩模式运行。</para>
    /// </summary>
    public static class LuaEnvHolder
    {
#if USE_XLUA
        private static LuaEnv _env;

        /// <summary>全局 Lua 环境（懒加载单例）。</summary>
        public static LuaEnv Env
        {
            get
            {
                if (_env == null)
                {
                    _env = new LuaEnv();
                    // 设置 require 搜索路径：热更优先 persistentDataPath → StreamingAssets/lua → 开发期 Assets/LuaScripts
                    _env.AddLoader(CustomLoader);
                }
                return _env;
            }
        }

        /// <summary>Lua require 自定义加载器（文档 14：热更后优先 persistentDataPath，其次包内 StreamingAssets，开发期读 Assets/LuaScripts）。</summary>
        private static byte[] CustomLoader(ref string filepath)
        {
            string name = filepath.Replace('.', '/') + ".lua";
            string[] roots =
            {
                Path.Combine(Application.persistentDataPath, "lua"),
                Path.Combine(Application.streamingAssetsPath, "lua"),
                Path.Combine(Application.dataPath, "LuaScripts"),
            };
            foreach (var root in roots)
            {
                string path = Path.Combine(root, name);
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
            }
            return null;
        }
#else
        /// <summary>未启用 XLua 时返回 null（桩模式）。</summary>
        public static object Env => null;
#endif

        /// <summary>释放全局环境（热更失败回滚 / 退出时调用）。</summary>
        public static void Dispose()
        {
#if USE_XLUA
            if (_env != null)
            {
                _env.Dispose();
                _env = null;
            }
#endif
        }
    }
}
