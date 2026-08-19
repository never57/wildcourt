using System;
using UnityEngine;
using UnityEngine.UI;

namespace WildCourt.UI.Yard
{
    /// <summary>
    /// 庭院 UI 通用工具（玩家信息入口 / 详情模态共用）。
    /// YardPanel 现有私有工具保持不动，新组件统一走这里。
    /// </summary>
    public static class YardUiKit
    {
        private static Font _font;

        public static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return go;
        }

        public static void SetAnchors(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static void SetStretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Image AddImage(GameObject go, Color color)
        {
            // 幂等：若节点已有 Image（如预制体已带）则复用，避免 AddComponent 因 Graphic 冲突返回 null 导致 NRE
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            if (img == null) return null;
            img.color = color;
            return img;
        }

        public static Text AddText(GameObject go, string content, int size, Color color)
        {
            // uGUI：一个 GameObject 只能有一个 Graphic，已有 Text 或添加失败时挂到子节点
            if (go.GetComponent<Text>() != null)
            {
                var child = CreateUiObject("text", go.transform);
                SetStretch(child);
                return AddText(child, content, size, color);
            }
            var text = go.AddComponent<Text>();
            if (text == null)
            {
                var child = CreateUiObject("text", go.transform);
                SetStretch(child);
                return AddText(child, content, size, color);
            }
            var font = LoadFont();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        public static Button AddButton(GameObject go, string label, Action onClick)
        {
            var img = AddImage(go, new Color(0.28f, 0.48f, 0.78f));
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var labelGo = CreateUiObject("label", go.transform);
            SetStretch(labelGo);
            AddText(labelGo, label, 14, Color.white);
            return btn;
        }

        public static void SetButtonWidth(Button btn, float width)
        {
            var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 22f;
        }

        /// <summary>白色圆形贴图（头像占位用）。</summary>
        public static Sprite CreateCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    pixels[y * size + x] = dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Font LoadFont()
        {
            if (_font != null) return _font;
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _font = f;
            return _font;
        }
    }
}
