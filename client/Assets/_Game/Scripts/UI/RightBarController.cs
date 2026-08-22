using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Game.Data;

namespace Game.UI
{
    /// <summary>
    /// 右侧功能栏控制器：仅「背包」「商店」2 个功能按钮，点击分发命令并做缩放回弹反馈。
    /// </summary>
    public class RightBarController : MonoBehaviour
    {
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();

        private void Awake()
        {
            // 按子物体 Txt_Label 的中文文本登记按钮（当前仅：背包/商店）
            foreach (Transform child in transform)
            {
                var btn = child.GetComponent<Button>();
                if (btn == null) continue;
                var label = child.GetComponentInChildren<Text>();
                string key = label != null && !string.IsNullOrEmpty(label.text)
                    ? label.text
                    : child.name.Replace("Item_", "");
                _buttons[key] = btn;
                string cmd = key;
                btn.onClick.AddListener(() => OnButtonClicked(cmd, btn));
            }

            ApplyStyle(UIStyleService.Current);
        }

        /// <summary>Step11：从 DT_UIStyle 注入条目尺寸/图标/标签字号（数据驱动，禁止硬编码）。</summary>
        public void ApplyStyle(UIStyleConfig s)
        {
            foreach (Transform child in transform)
            {
                var label = child.GetComponentInChildren<Text>();
                if (label != null) label.fontSize = s.rightLabelSize;

                var rt = (RectTransform)child;
                rt.sizeDelta = s.rightItemSize;
                var le = child.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.preferredWidth = s.rightItemSize.x;
                    le.preferredHeight = s.rightItemSize.y;
                }

                var icon = child.Find("Img_Icon");
                if (icon != null)
                    ((RectTransform)icon).sizeDelta = new Vector2(s.rightIconSize, s.rightIconSize);
            }
        }

        private void OnEnable()
        {
            UICommands.OnShopHighlight += OnShopHighlighted;
        }

        private void OnDisable()
        {
            UICommands.OnShopHighlight -= OnShopHighlighted;
        }

        private void OnShopHighlighted()
        {
            if (_buttons.TryGetValue("商店", out var shopBtn))
                shopBtn.transform.DOPunchScale(Vector3.one * 0.15f, 0.6f, 10, 0.5f).SetLink(shopBtn.gameObject);
        }

        private void OnButtonClicked(string cmd, Button btn)
        {
            // 点击反馈：缩放回弹 0.1s
            btn.transform.DOPunchScale(Vector3.one * 0.12f, 0.1f, 3, 0.3f).SetLink(gameObject);

            switch (cmd)
            {
                case "背包": UICommands.OpenBag(); break;
                case "商店": UICommands.OpenShop(); break;
                default:      Debug.Log($"[UI] TODO: {cmd}"); break;
            }
        }

    }
}
