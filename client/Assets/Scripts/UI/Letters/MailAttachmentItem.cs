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
        [SerializeField] private GameObject statusImage;       // 领取状态图（Status 子节点，领取后置 true 显示）

        [Header("本地图标映射（按 rewardType）")]
        [SerializeField] private Sprite shellIcon;   // 庭贝
        [SerializeField] private Sprite couponIcon;  // 邮券
        [SerializeField] private Sprite itemIcon;    // 道具/非遗

        /// <summary>绑定附件：按 rewardType 设置图标、名称、数量，并按领取状态控制 Status 图片显隐。</summary>
        /// <param name="rewardType">shell / coupon / item / 空</param>
        /// <param name="count">数量</param>
        /// <param name="itemId">道具/非遗 ID（item 类型时用于名称回退）</param>
        /// <param name="claimed">所属信件是否已领取（true 时显示 Status 图片）</param>
        public void Bind(string rewardType, int count, string itemId, bool claimed = false)
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
            // 领取状态图片：仅已领取时显示
            if (statusImage != null) statusImage.SetActive(claimed);
        }

        private void SetVisual(Sprite icon, string name, int count)
        {
            if (iconImage != null)
            {
                // 图标字段已配置则替换 sprite；未配置（null）则保留 Items 预制体默认 sprite，不隐藏
                if (icon != null) iconImage.sprite = icon;
                iconImage.gameObject.SetActive(true);
            }
            if (typeText != null) typeText.text = name;
            if (countText != null) countText.text = count > 0 ? "x" + count : string.Empty;
        }
    }
}
