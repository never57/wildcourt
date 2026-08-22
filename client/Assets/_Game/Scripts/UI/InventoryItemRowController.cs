using UnityEngine;
using UnityEngine.UI;
using Game.Data;

namespace Game.UI
{
    /// <summary>
    /// 背包行项：绑定鱼饵/鱼配置，处理"设为当前鱼饵 / 使用 / 出售"。
    /// </summary>
    public class InventoryItemRowController : MonoBehaviour
    {
        [SerializeField] Image imgFrame;
        [SerializeField] Image imgIcon;
        [SerializeField] Text txtName;
        [SerializeField] Text txtDesc;
        [SerializeField] Text txtCount;
        [SerializeField] Button btnPrimary;
        [SerializeField] Text txtPrimary;
        [SerializeField] Button btnSell;
        [SerializeField] Text txtSell;
        [SerializeField] Sprite frameNormal;   // PH_Frame_Normal（构建脚本绑定）
        [SerializeField] Sprite frameRare;     // PH_Frame_Rare

        private BaitConfig _bait;
        private FishConfig _fish;
        private int _count;

        public enum RowAction { SelectBait, Use, Sell }

        /// <summary>点击回调（面板订阅），参数：本行 + 动作类型。</summary>
        public System.Action<InventoryItemRowController, RowAction> OnAction;

        public string ItemId => _bait != null ? _bait.id : (_fish != null ? _fish.id : null);
        public bool IsBait => _bait != null;
        public FishConfig Fish => _fish;
        public BaitConfig Bait => _bait;
        public int Count => _count;

        private void Awake()
        {
            ApplyStyle(UIStyleService.Current);
        }

        /// <summary>Step11：从 DT_UIStyle 注入字号与尺寸（数据驱动，禁止硬编码）。</summary>
        public void ApplyStyle(UIStyleConfig s)
        {
            if (txtName != null) txtName.fontSize = s.bodySize;
            if (txtDesc != null) txtDesc.fontSize = s.descSize;
            if (txtCount != null) txtCount.fontSize = s.descSize;
            if (txtPrimary != null) txtPrimary.fontSize = s.descSize;
            if (txtSell != null) txtSell.fontSize = s.descSize;
            if (imgIcon != null)
                ((RectTransform)imgIcon.transform).sizeDelta = new Vector2(s.bagIconSize, s.bagIconSize);
            if (imgFrame != null)
                ((RectTransform)imgFrame.transform).sizeDelta = new Vector2(s.bagFrameSize, s.bagFrameSize);
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, s.bagRowHeight);
        }

        private void Start()
        {
            btnPrimary.onClick.AddListener(() => OnAction?.Invoke(this, RowAction.SelectBait));
            btnSell.onClick.AddListener(() => OnAction?.Invoke(this, RowAction.Sell));
        }

        public void BindBait(BaitConfig bait, int count, bool selected)
        {
            _bait = bait;
            _fish = null;
            _count = count;

            if (imgFrame != null) imgFrame.sprite = frameNormal;
            if (imgIcon != null) imgIcon.sprite = bait != null ? bait.icon : null;
            if (txtName != null)
            {
                txtName.text = bait != null ? bait.displayName : "";
                txtName.color = new Color(0.15f, 0.12f, 0.08f, 1f);
            }
            if (txtDesc != null) txtDesc.text = bait != null ? bait.description : "";
            if (txtCount != null) txtCount.text = "×" + count;

            // 主按钮：使用中 / 设为当前鱼饵
            if (btnPrimary != null)
            {
                if (selected)
                {
                    txtPrimary.text = "使用中";
                    btnPrimary.interactable = false;
                }
                else
                {
                    txtPrimary.text = "设为鱼饵";
                    btnPrimary.interactable = true;
                }
            }
            // 鱼饵不提供出售
            if (btnSell != null) btnSell.gameObject.SetActive(false);
        }

        public void BindFish(FishConfig fish, int count)
        {
            _fish = fish;
            _bait = null;
            _count = count;

            bool rare = fish != null && fish.rarity == Rarity.Rare;
            if (imgFrame != null) imgFrame.sprite = rare ? frameRare : frameNormal;
            if (imgIcon != null) imgIcon.sprite = fish != null ? fish.icon : null;
            if (txtName != null)
            {
                txtName.text = fish != null ? fish.displayName : "";
                txtName.color = rare ? new Color(0.85f, 0.65f, 0.10f, 1f) : new Color(0.15f, 0.12f, 0.08f, 1f);
            }
            if (txtDesc != null) txtDesc.text = fish != null ? fish.usage : "";
            if (txtCount != null) txtCount.text = "×" + count;

            // 主按钮：使用（预留，视觉置灰但可点）
            if (btnPrimary != null)
            {
                txtPrimary.text = "使用";
                btnPrimary.interactable = true;
                var colors = btnPrimary.colors;
                colors.normalColor = new Color(0.75f, 0.75f, 0.75f, 1f);
                btnPrimary.colors = colors;
            }
            // 出售
            if (btnSell != null)
            {
                btnSell.gameObject.SetActive(true);
                txtSell.text = fish != null ? $"出售 +{fish.sellPrice}" : "";
            }
        }

    }
}
