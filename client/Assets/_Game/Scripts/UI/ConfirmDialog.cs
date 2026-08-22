using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 游戏内确认对话框（正式功能，供“背包出售”等二次确认使用）。
    /// 纯代码动态构建 UGUI，不依赖预制体；点击遮罩/取消即关闭，确认后执行回调。
    /// </summary>
    public class ConfirmDialog : MonoBehaviour
    {
        static ConfirmDialog _instance;
        Text _msg;
        Button _ok;
        System.Action _onConfirm;

        /// <summary>弹出确认框。onConfirm 为确认后回调；取消/遮罩关闭不触发。</summary>
        public static void Show(string message, System.Action onConfirm, string okText = "确认")
        {
            if (_instance == null)
            {
                var canvas = Object.FindObjectOfType<Canvas>(true);
                if (canvas == null) return;
                var go = new GameObject("ConfirmDialog", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                _instance = go.AddComponent<ConfirmDialog>();
                _instance.Build();
            }
            _instance.gameObject.SetActive(true);
            _instance.transform.SetAsLastSibling();
            _instance._msg.text = message;
            _instance._ok.GetComponentInChildren<Text>().text = okText;
            _instance._onConfirm = onConfirm;
        }

        void Build()
        {
            var root = transform as RectTransform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            // 遮罩：点击取消
            var maskGo = new GameObject("Mask", typeof(RectTransform));
            maskGo.transform.SetParent(transform, false);
            var maskRt = (RectTransform)maskGo.transform;
            maskRt.anchorMin = Vector2.zero;
            maskRt.anchorMax = Vector2.one;
            maskRt.offsetMin = Vector2.zero;
            maskRt.offsetMax = Vector2.zero;
            var maskImg = maskGo.AddComponent<Image>();
            maskImg.color = new Color(0f, 0f, 0f, 0.65f);
            var maskBtn = maskGo.AddComponent<Button>();
            maskBtn.transition = Selectable.Transition.None;
            maskBtn.onClick.AddListener(Close);

            // 面板
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(480f, 260f);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.15f, 0.12f, 0.08f, 0.98f);

            // 消息文本
            var msgGo = new GameObject("Txt_Message", typeof(RectTransform));
            msgGo.transform.SetParent(panel.transform, false);
            var msgRt = (RectTransform)msgGo.transform;
            msgRt.anchorMin = new Vector2(0f, 0f);
            msgRt.anchorMax = new Vector2(1f, 1f);
            msgRt.offsetMin = new Vector2(30f, 80f);
            msgRt.offsetMax = new Vector2(-30f, -20f);
            _msg = msgGo.AddComponent<Text>();
            _msg.font = LoadFont();
            _msg.fontSize = 32;
            _msg.alignment = TextAnchor.MiddleCenter;
            _msg.color = new Color(1f, 1f, 1f, 1f);
            _msg.raycastTarget = false;
            _msg.horizontalOverflow = HorizontalWrapMode.Wrap;
            _msg.verticalOverflow = VerticalWrapMode.Overflow;

            // 按钮区
            var btnRow = new GameObject("HGroup_Buttons", typeof(RectTransform));
            btnRow.transform.SetParent(panel.transform, false);
            var rowRt = (RectTransform)btnRow.transform;
            rowRt.anchorMin = new Vector2(0f, 0f);
            rowRt.anchorMax = new Vector2(1f, 0f);
            rowRt.pivot = new Vector2(0.5f, 0f);
            rowRt.sizeDelta = new Vector2(0f, 76f);
            rowRt.anchoredPosition = new Vector2(0f, 24f);

            CreateButton(btnRow.transform, "Btn_Cancel", "取消", 0.45f, Close);
            _ok = CreateButton(btnRow.transform, "Btn_Confirm", "确认", 0.87f, () => { var cb = _onConfirm; Close(); cb?.Invoke(); });
        }

        Button CreateButton(Transform parent, string name, string label, float hue, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(180f, 76f);
            var img = go.AddComponent<Image>();
            img.color = new Color(hue, 0.45f, 0.12f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f),
                pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
            btn.onClick.AddListener(onClick);

            var txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = (RectTransform)txtGo.transform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.font = LoadFont();
            txt.fontSize = 32;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = label;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return btn;
        }

        Font LoadFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        void Close()
        {
            _onConfirm = null;
            gameObject.SetActive(false);
        }
    }
}
