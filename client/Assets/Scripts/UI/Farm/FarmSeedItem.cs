using UnityEngine;
using UnityEngine.UI;

namespace WildCourt.UI.Farm
{
    /// <summary>种子项（seedItemPrefab 组件，字段在预制体中绑定）。</summary>
    public class FarmSeedItem : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text priceText;
        [SerializeField] private Button button;

        public void Bind(string name, int price, System.Action onClick)
        {
            if (nameText != null) nameText.text = name;
            if (priceText != null) priceText.text = price.ToString();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick());
            }
        }
    }
}
