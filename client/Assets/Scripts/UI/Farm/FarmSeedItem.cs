using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WildCourt.UI.Farm
{
    /// <summary>种子卡片（seedItem.prefab 组件）：种子名 + 持有数量，点击种植。</summary>
    public class FarmSeedItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;   // 显示持有数量（xN）
        [SerializeField] private Button button;

        public void Bind(string name, int count, Action onClick)
        {
            if (nameText != null) nameText.text = name;
            if (priceText != null) priceText.text = "x" + count;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }
    }
}
