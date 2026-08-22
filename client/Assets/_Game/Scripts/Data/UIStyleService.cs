using UnityEngine;

namespace Game.Data
{
    /// <summary>运行期 UI 样式单例（由 GameUIBootstrap 注册 DT_UIStyle，控制器 Awake 读取注入）。</summary>
    public static class UIStyleService
    {
        private static UIStyleConfig _current;

        public static UIStyleConfig Current
        {
            get
            {
                if (_current == null)
                {
                    // 兜底：未注册时用默认值（即 Step11 目标规格），保证运行期不依赖资产
                    _current = ScriptableObject.CreateInstance<UIStyleConfig>();
                    _current.name = "UIStyleConfig-Default";
                }
                return _current;
            }
        }

        public static void Register(UIStyleConfig style)
        {
            if (style != null) _current = style;
        }
    }
}
