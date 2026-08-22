using UnityEngine;
using UnityEngine.UI;
using Game.Data;

namespace Game.UI
{
    /// <summary>
    /// 弹窗左上角关闭按钮统一样式（需求：≥40x40、高对比度、严格左上角、任意分辨率可见）。
    /// 商店/背包面板在 ApplyStyle 时调用；无图标（无子 Text）时自动补建“×”。
    /// </summary>
    public static class CloseButtonHelper
    {
        public static void Apply(Button btn, UIStyleConfig s)
        {
            if (btn == null) return;
            var rt = btn.transform as RectTransform;
            if (rt == null) return;

            // 尺寸：不小于 40x40（资产可配置，默认 64）
            float size = Mathf.Max(40f, s != null ? s.closeButtonSize : 64f);
            rt.sizeDelta = new Vector2(size, size);

            // 位置：严格左上角，留内边距
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            float ox = s != null ? s.closeButtonOffset.x : 24f;
            float oy = s != null ? s.closeButtonOffset.y : 24f;
            rt.anchoredPosition = new Vector2(ox, -oy);
            rt.SetAsLastSibling(); // 置顶，避免被其他 UI 遮挡

            // 高对比底色
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = s != null ? s.closeButtonBgColor : new Color(0.15f, 0.12f, 0.08f, 0.95f);
                img.raycastTarget = true;
            }

            // 保证有可见 “×” 标记：无子 Text 则创建，有则统一样式
            Text mark = btn.GetComponentInChildren<Text>(true);
            if (mark == null)
            {
                var go = new GameObject("Txt_Close", typeof(RectTransform));
                go.transform.SetParent(btn.transform, false);
                mark = go.AddComponent<Text>();
                var mrt = (RectTransform)mark.transform;
                mrt.anchorMin = Vector2.zero;
                mrt.anchorMax = Vector2.one;
                mrt.offsetMin = Vector2.zero;
                mrt.offsetMax = Vector2.zero;
            }
            mark.text = "×";
            mark.fontStyle = FontStyle.Bold;
            mark.alignment = TextAnchor.MiddleCenter;
            mark.fontSize = Mathf.Max(28, s != null ? (int)s.closeButtonTextSize : 40);
            mark.color = s != null ? s.closeButtonMarkColor : Color.white;
            mark.raycastTarget = false;
            if (mark.font == null)
            {
                var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (f != null) mark.font = f;
            }
        }
    }
}
