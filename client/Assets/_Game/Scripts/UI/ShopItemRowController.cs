using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Game.Data;
using Game.Services;

namespace Game.UI
{
    /// <summary>
    /// 商店行项：数量选择（1~99）、实时总价、购买按钮可用性（货币 + 堆叠上限 999）。
    /// </summary>
    public class ShopItemRowController : MonoBehaviour
    {
        public const int MaxStack = 999;

        [SerializeField] Image imgIcon;
        [SerializeField] Text txtName;
        [SerializeField] Text txtDesc;
        [SerializeField] Text txtUnitPrice;
        [SerializeField] Button btnMinus;
        [SerializeField] Button btnPlus;
        [SerializeField] Text txtQty;
        [SerializeField] Button btnX1;
        [SerializeField] Button btnX5;
        [SerializeField] Button btnX10;
        [SerializeField] Text txtTotal;
        [SerializeField] Button btnBuy;

        private BaitConfig _bait;
        private int _qty = 1;

        public int CurrentQty => _qty;
        public string TotalPriceText => txtTotal != null ? txtTotal.text : "";
        public bool BuyInteractable => btnBuy != null && btnBuy.interactable;
        public BaitConfig Bait => _bait;

        public void Bind(BaitConfig bait)
        {
            _bait = bait;
            if (bait == null) return;

            if (imgIcon != null) imgIcon.sprite = bait.icon;
            if (txtName != null) txtName.text = bait.displayName;
            if (txtDesc != null) txtDesc.text = bait.description;
            if (txtUnitPrice != null) txtUnitPrice.text = "单价 " + bait.price + " 庭贝";

            _qty = 1;
            Refresh();
        }

        private void Awake()
        {
            ApplyStyle(UIStyleService.Current);
        }

        /// <summary>Step11：从 DT_UIStyle 注入字号与尺寸（数据驱动，禁止硬编码）。</summary>
        public void ApplyStyle(UIStyleConfig s)
        {
            if (txtName != null) txtName.fontSize = s.bodySize;
            if (txtDesc != null) txtDesc.fontSize = s.descSize;
            if (txtUnitPrice != null) txtUnitPrice.fontSize = s.descSize;
            if (txtQty != null) txtQty.fontSize = s.descSize;
            if (txtTotal != null) txtTotal.fontSize = s.descSize;
            if (btnBuy != null)
                ((RectTransform)btnBuy.transform).sizeDelta = s.buyButtonSize;
            foreach (var b in new[] { btnMinus, btnPlus, btnX1, btnX5, btnX10 })
            {
                if (b != null)
                    ((RectTransform)b.transform).sizeDelta = s.qtyButtonSize;
            }
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, s.shopRowHeight);
        }

        private void Start()
        {
            if (btnMinus != null) btnMinus.onClick.AddListener(() => ChangeQty(-1));
            if (btnPlus != null) btnPlus.onClick.AddListener(() => ChangeQty(1));
            if (btnX1 != null) btnX1.onClick.AddListener(() => SetQty(1));
            if (btnX5 != null) btnX5.onClick.AddListener(() => SetQty(5));
            if (btnX10 != null) btnX10.onClick.AddListener(() => SetQty(10));
            if (btnBuy != null) btnBuy.onClick.AddListener(TryBuy);
        }

        private void ChangeQty(int delta)
        {
            SetQty(_qty + delta);
        }

        public void SetQty(int qty)
        {
            _qty = Mathf.Clamp(qty, 1, 99);
            Refresh();
        }

        public void RefreshBuyState() => UpdateBuyInteractable();

        private void Refresh()
        {
            if (txtQty != null) txtQty.text = _qty.ToString();
            int total = _bait != null ? _bait.price * _qty : 0;
            if (txtTotal != null) txtTotal.text = total.ToString();
            UpdateBuyInteractable();
        }

        private void UpdateBuyInteractable()
        {
            if (btnBuy == null || _bait == null) return;
            int total = _bait.price * _qty;
            bool enoughMoney = InventoryService.Instance.Currency >= total;
            bool enoughStack = InventoryService.Instance.GetCount(_bait.id) + _qty <= MaxStack;
            bool can = enoughMoney && enoughStack;
            btnBuy.interactable = can;
            var cg = btnBuy.GetComponent<CanvasGroup>();
            if (cg == null) cg = btnBuy.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = can ? 1f : 0.5f;
        }

        /// <summary>购买：余额/堆叠校验 → 扣款 → 入背包 → 刷新。</summary>
        private void TryBuy()
        {
            if (_bait == null) return;

            // 堆叠上限校验
            if (InventoryService.Instance.GetCount(_bait.id) + _qty > MaxStack)
            {
                ToastManager.Instance?.Show("背包已满（单种上限999）");
                var rt = (RectTransform)transform;
                rt.DOShakeAnchorPos(0.3f, 8f, 14, 90f, false, true).SetLink(gameObject);
                return;
            }

            int total = _bait.price * _qty;
            if (!InventoryService.Instance.TrySpendCurrency(total))
            {
                ToastManager.Instance?.Show("庭贝不足");
                var rt = (RectTransform)transform;
                rt.DOShakeAnchorPos(0.3f, 8f, 14, 90f, false, true).SetLink(gameObject);
                return;
            }

            InventoryService.Instance.AddItem(_bait.id, _qty);
            ToastManager.Instance?.Show($"购买成功【{_bait.displayName}】×{_qty}");
            Refresh();
        }
    }
}
