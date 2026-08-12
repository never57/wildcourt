using UnityEngine;
using UnityEngine.EventSystems;
using WildCourt.Gameplay.Yard;

namespace WildCourt.UI.Yard
{
    /// <summary>
    /// 装饰格子拖拽：长按触发拖放放置（阈值见 YardRenderer.LongPressSeconds），短按仅提示。
    /// 挂载到 cell 预制体的 dragCell 子节点上，需配合 Image（透明亦可）接收射线。
    /// </summary>
    public class YardItemCell : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private YardPanel _panel;
        private DecorationConfig _config;
        private bool _pressed;
        private float _downTime;

        public void Init(YardPanel panel, DecorationConfig config)
        {
            _panel = panel;
            _config = config;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            _downTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            if (_panel != null) _panel.OnCellRelease(_config, _downTime);
        }

        private void Update()
        {
            if (_pressed && _panel != null && Time.unscaledTime - _downTime >= YardRenderer.LongPressSeconds)
            {
                _pressed = false;
                _panel.OnCellLongPress(_config);
            }
        }
    }
}
