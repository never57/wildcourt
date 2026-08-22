using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 鱼线视图：竿尖→中点→浮漂两段折线，中点向下 sag 偏移模拟垂线弧度。
    /// 每帧由控制器调用 Refresh（canvasRoot 空间换算）。
    /// </summary>
    public class FishingLineView : MonoBehaviour
    {
        [SerializeField] RectTransform segmentTop;      // 竿尖 → 中点
        [SerializeField] RectTransform segmentBottom;   // 中点 → 浮漂
        [SerializeField] RectTransform canvasRoot;      // Canvas_UI
        [SerializeField] RectTransform rodTipAnchor;    // 竿尖 world 锚点
        [SerializeField] RectTransform floatAnchor;     // 浮漂 world 锚点
        [SerializeField] float sag = 40f;               // 中点下沉量

        private void Awake()
        {
            if (canvasRoot == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) canvasRoot = canvas.transform as RectTransform;
            }
        }

        /// <summary>运行时由控制器绑定竿尖 / 浮漂锚点。</summary>
        public void SetRodTip(RectTransform anchor) => rodTipAnchor = anchor;
        public void SetFloatAnchor(RectTransform anchor) => floatAnchor = anchor;

        public void Show()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide() => gameObject.SetActive(false);

        public void Refresh()
        {
            if (!gameObject.activeSelf) return;
            if (segmentTop == null || segmentBottom == null || canvasRoot == null) return;

            Vector2 a = WorldToLocal(rodTipAnchor != null ? rodTipAnchor.position : canvasRoot.position);
            Vector2 b = WorldToLocal(floatAnchor != null ? floatAnchor.position : canvasRoot.position);
            Vector2 mid = (a + b) * 0.5f + new Vector2(0f, -sag);
            Place(segmentTop, a, mid);
            Place(segmentBottom, mid, b);
        }

        private Vector2 WorldToLocal(Vector3 world)
        {
            Vector3 local = canvasRoot.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }

        private static void Place(RectTransform seg, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float len = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            seg.anchorMin = seg.anchorMax = new Vector2(0.5f, 0.5f);
            seg.pivot = new Vector2(0f, 0.5f);
            seg.anchoredPosition = from;
            seg.sizeDelta = new Vector2(len, 3f);
            seg.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
