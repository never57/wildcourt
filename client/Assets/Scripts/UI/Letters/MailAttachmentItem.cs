using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WildCourt.UI.Letters
{
    /// <summary>
    /// 邮件附件格子组件（挂在 Items.prefab 上）。
    /// 按 rewardType 本地映射图标与名称展示附件：庭贝 / 邮券 / 道具（非遗）。
    /// </summary>
    public class MailAttachmentItem : MonoBehaviour
    {
        [Header("子节点引用（Items.prefab 内）")]
        [SerializeField] private Image iconImage;              // 图标（Image 子节点）
        [SerializeField] private TextMeshProUGUI typeText;     // 名称（type 子节点，TMP）
        [SerializeField] private TextMeshProUGUI countText;    // 数量（count 子节点，TMP）

        [Header("本地图标映射（按 rewardType）")]
        [SerializeField] private Sprite shellIcon;   // 庭贝
        [SerializeField] private Sprite couponIcon;  // 邮券
        [SerializeField] private Sprite itemIcon;    // 道具/非遗

        /// <summary>绑定附件：按 rewardType 设置图标、名称、数量。</summary>
        /// <param name="rewardType">shell / coupon / item / 空</param>
        /// <param name="count">数量</param>
        /// <param name="itemId">道具/非遗 ID（item 类型时用于名称回退）</param>
        public void Bind(string rewardType, int count, string itemId)
        {
            switch (rewardType)
            {
                case "shell":
                    SetVisual(shellIcon, "庭贝", count);
                    break;
                case "coupon":
                    SetVisual(couponIcon, "邮券", count);
                    break;
                case "item":
                    SetVisual(itemIcon, string.IsNullOrEmpty(itemId) ? "道具" : itemId, count);
                    break;
                default:
                    SetVisual(null, "无附件", 0);
                    break;
            }
        }

        private void SetVisual(Sprite icon, string name, int count)
        {
            if (iconImage != null)
            {
                if (icon != null) iconImage.sprite = icon;
                iconImage.gameObject.SetActive(icon != null);
            }
            if (typeText != null) typeText.text = name;
            if (countText != null) countText.text = count > 0 ? "x" + count : string.Empty;
        }
    }
}
