using UnityEngine;
using UnityEngine.UI;

namespace WildCourt.UI.Farm
{
    /// <summary>农田格（cellPrefab 组件，字段在预制体中绑定）。</summary>
    public class FarmCell : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text timeText;
        [SerializeField] private Button button;
        private int _cellId;
        private System.Action<int> _onClick;

        public void Bind(int cellId, System.Action<int> onClick)
        {
            _cellId = cellId;
            _onClick = onClick;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke(_cellId));
            }
        }

        public void SetView(string name, string time, Color color)
        {
            if (nameText != null) nameText.text = name;
            if (timeText != null) timeText.text = time;
            var img = GetComponent<Image>();
            if (img != null) img.color = color;
        }
    }
}
