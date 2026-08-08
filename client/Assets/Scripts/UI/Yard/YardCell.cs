using System;
using UnityEngine;
using UnityEngine.UI;
using WildCourt.Gameplay.Yard;

namespace WildCourt.UI.Yard
{
    /// <summary>
    /// 商品格子模板组件（YardPanel 预制体中的 cellPrefab，提前摆放）。
    /// Bind 负责填数据：名称 / 已建造-拥有 / 剩余-已满 / 购买按钮 / 商品图异步加载。
    /// </summary>
    public class YardCell : MonoBehaviour
    {
        [SerializeField] private Image bgImage;      // 背景（无商品图时的底色）
        [SerializeField] private Image iconImage;    // 商品图（异步加载后替换）
        [SerializeField] private Text nameText;
        [SerializeField] private Text countText;     // 已建造 X / 拥有 Y
        [SerializeField] private Text remainText;    // 剩余 N / 已满
        [SerializeField] private Button buyButton;
        [SerializeField] private YardItemCell dragCell; // 长按拖放（仅已拥有启用）

        public void Bind(DecorationConfig d, int owned, int built, int remain, Action onBuy, Color bgColor, bool ownedFlag)
        {
            if (bgImage != null) bgImage.color = bgColor;
            if (nameText != null) nameText.text = d.name;
            if (countText != null) countText.text = $"已建造 {built}/{owned}";

            bool canBuy = remain > 0;
            if (remainText != null) remainText.text = canBuy ? $"剩余 {remain}" : "已满";

            if (buyButton != null)
            {
                buyButton.gameObject.SetActive(canBuy);
                if (canBuy)
                {
                    buyButton.onClick.RemoveAllListeners();
                    buyButton.onClick.AddListener(() => onBuy?.Invoke());
                }
            }

            if (dragCell != null) dragCell.gameObject.SetActive(ownedFlag);

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = ownedFlag ? Color.white : new Color(0.62f, 0.62f, 0.62f, 0.9f);
                GoodsSprites.Request(d.id, (id, sprite) =>
                {
                    if (sprite == null || iconImage == null) return;
                    iconImage.sprite = sprite;
                    iconImage.color = ownedFlag ? Color.white : new Color(0.62f, 0.62f, 0.62f, 0.9f);
                });
            }
        }
    }
}