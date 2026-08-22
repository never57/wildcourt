using System;
using UnityEngine;
using UnityEngine.UI;
using WildCourt.Gameplay.Fishing;

namespace WildCourt.UI.Fishing
{
    /// <summary>
    /// 选饵阶段单个鱼饵槽位（挂于 baitItemPrefab 上）。
    /// 展示名称 / 庭贝售价 / 持有数量；持有不足置灰并显示「去商店购买」。
    /// </summary>
    public class BaitSlot : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text priceText;
        [SerializeField] private Text countText;
        [SerializeField] private Button button;
        [SerializeField] private GameObject shopJumpGo; // 持有不足时才显示

        private string _baitId;
        private BaitData _bait;
        private Action<string> _onClick;    // 点击抛竿
        private Action<string> _onShopJump; // 去商店购买

        /// <summary>绑定数据与回调。持有不足时点击触发商店跳转，否则抛竿。</summary>
        public void Bind(BaitData bait, int owned, Action<string> onClick, Action<string> onShopJump)
        {
            _bait = bait;
            _baitId = bait.id;
            _onClick = onClick;
            _onShopJump = onShopJump;

            if (nameText != null) nameText.text = bait.name;
            if (priceText != null) priceText.text = "庭贝 " + bait.price;
            if (button != null) button.onClick.RemoveAllListeners();
            SetCount(owned, bait);
        }

        /// <summary>刷新持有数量与灰显/商店按钮状态。</summary>
        public void SetCount(int owned, BaitData bait)
        {
            if (countText != null) countText.text = "x" + owned;
            bool enough = owned > 0;

            // 不足 → 灰显 + 显示「去商店购买」跳转
            if (button != null)
            {
                var cb = button.colors;
                var normal = enough ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
                cb.normalColor = normal;
                cb.highlightedColor = enough ? normal * 1.1f : normal;
                cb.selectedColor = normal;
                cb.pressedColor = normal * 0.9f;
                button.colors = cb;
            }
            if (shopJumpGo != null) shopJumpGo.SetActive(!enough);

            // 点击行为：不足 → 商店；足 → 抛竿
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (enough)
                {
                    button.onClick.AddListener(() => _onClick?.Invoke(_baitId));
                }
                else
                {
                    button.onClick.AddListener(() => _onShopJump?.Invoke(_baitId));
                }
            }
        }
    }
}