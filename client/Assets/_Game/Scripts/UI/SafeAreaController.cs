using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Step11 分辨率适配：按 Screen.safeArea 收缩 UI，兼容锚点拉伸与单点定位两种 RectTransform。
    /// 支持四条边独立配置；safeArea 变化（旋转/刘海）时自动刷新。
    /// </summary>
    [DisallowMultipleComponent]
    public class SafeAreaController : MonoBehaviour
    {
        [SerializeField] private bool fitTop;
        [SerializeField] private bool fitBottom;
        [SerializeField] private bool fitLeft;
        [SerializeField] private bool fitRight;

        private RectTransform _rt;
        private Rect _lastSafe;
        private bool _baseSet;
        private Vector2 _basePos;
        private Vector2 _baseOffsetMin;
        private Vector2 _baseOffsetMax;

        /// <summary>由 Applier / 代码配置收缩边。</summary>
        public void Configure(bool top, bool bottom, bool left, bool right)
        {
            fitTop = top;
            fitBottom = bottom;
            fitLeft = left;
            fitRight = right;
            _baseSet = false;
            Apply();
        }

        private void Awake()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        private void OnEnable()
        {
            _lastSafe = default;
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafe)
                Apply();
        }

        private void Apply()
        {
            if (_rt == null)
                _rt = (RectTransform)transform;
            _lastSafe = Screen.safeArea;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas == null)
                return;
            var rootRT = canvas.rootCanvas.transform as RectTransform;
            if (rootRT == null)
                return;

            // 记录基准（prefab/Applier 写入的原始布局）
            if (!_baseSet)
            {
                _basePos = _rt.anchoredPosition;
                _baseOffsetMin = _rt.offsetMin;
                _baseOffsetMax = _rt.offsetMax;
                _baseSet = true;
            }
            _rt.anchoredPosition = _basePos;
            _rt.offsetMin = _baseOffsetMin;
            _rt.offsetMax = _baseOffsetMax;

            float w = rootRT.rect.width;
            float h = rootRT.rect.height;
            if (w <= 0f || h <= 0f)
                return;

            Rect sa = Screen.safeArea;
            float top = Mathf.Max(0f, (Screen.height - sa.yMax) * h / Mathf.Max(1, Screen.height));
            float bottom = Mathf.Max(0f, sa.yMin * h / Mathf.Max(1, Screen.height));
            float left = Mathf.Max(0f, sa.xMin * w / Mathf.Max(1, Screen.width));
            float right = Mathf.Max(0f, (Screen.width - sa.xMax) * w / Mathf.Max(1, Screen.width));

            float inL = fitLeft ? left : 0f;
            float inT = fitTop ? top : 0f;
            float inR = fitRight ? right : 0f;
            float inB = fitBottom ? bottom : 0f;

            if (_rt.anchorMin == _rt.anchorMax)
            {
                // 单点锚定：按锚点所在象限方向收缩
                var p = _rt.anchoredPosition;
                if (Mathf.Approximately(_rt.anchorMin.x, 0f)) p.x += inL;
                else if (Mathf.Approximately(_rt.anchorMin.x, 1f)) p.x -= inR;
                if (Mathf.Approximately(_rt.anchorMin.y, 0f)) p.y += inB;
                else if (Mathf.Approximately(_rt.anchorMin.y, 1f)) p.y -= inT;
                _rt.anchoredPosition = p;
            }
            else
            {
                // 拉伸锚定：收缩 offset
                _rt.offsetMin = new Vector2(_baseOffsetMin.x + inL, _baseOffsetMin.y + inB);
                _rt.offsetMax = new Vector2(_baseOffsetMax.x - inR, _baseOffsetMax.y - inT);
            }
        }
    }
}
