using System;
using DG.Tweening;
using Game.Data;
using Game.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 顶栏控制器：货币/鱼饵数量实时刷新，订阅 InventoryService 事件。
    /// 兼容订阅时 Service 未 Init：未初始化跳过读取，Bootstrap Init 后靠事件触发 RefreshAll。
    /// </summary>
    public class TopBarController : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private Text txtCurrency;
        [SerializeField] private Text txtBait;
        [SerializeField] private Text txtSceneName;
        [SerializeField] private Text txtTimeOfDay;

        [Header("Buttons")]
        [SerializeField] private Button btnCurrencyPlus;
        [SerializeField] private Button btnBaitPlus;

        private bool _subscribed;

        public string CurrentCurrencyText => txtCurrency != null ? txtCurrency.text : "";
        public string CurrentBaitText => txtBait != null ? txtBait.text : "";

        private void OnEnable()
        {
            Subscribe();
            RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            var inv = InventoryService.Instance;
            inv.OnCurrencyChanged += HandleCurrencyChanged;
            inv.OnItemCountChanged += HandleItemCountChanged;
            inv.OnSelectedBaitChanged += HandleSelectedBaitChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            var inv = InventoryService.Instance;
            inv.OnCurrencyChanged -= HandleCurrencyChanged;
            inv.OnItemCountChanged -= HandleItemCountChanged;
            inv.OnSelectedBaitChanged -= HandleSelectedBaitChanged;
            _subscribed = false;
        }

        private void HandleCurrencyChanged(int currency)
        {
            if (txtCurrency == null) return;
            SetText(txtCurrency, currency.ToString());
        }

        private void HandleItemCountChanged(string itemId, int count)
        {
            // 鱼饵文本 = 当前选中鱼饵的持有数量
            var inv = InventoryService.Instance;
            if (inv.SelectedBait == null || itemId != inv.SelectedBait.id) return;
            if (txtBait == null) return;
            SetText(txtBait, count.ToString());
        }

        private void HandleSelectedBaitChanged(BaitConfig bait)
        {
            if (bait == null || txtBait == null) return;
            SetText(txtBait, InventoryService.Instance.GetCount(bait.id).ToString());
        }

        private void RefreshAll()
        {
            var inv = InventoryService.Instance;
            // 兼容未 Init：无数据表或选中鱼饵为空时跳过读取
            if (inv.SelectedBait == null) return;
            if (txtCurrency != null) SetText(txtCurrency, inv.Currency.ToString());
            if (txtBait != null) SetText(txtBait, inv.GetCount(inv.SelectedBait.id).ToString());
        }

        private void SetText(Text t, string v)
        {
            if (t.text == v) return;
            t.text = v;
            // 数量变化缩放脉冲 0.15s
            t.transform.DOPunchScale(Vector3.one * 0.15f, 0.15f, 3, 0.3f)
                .SetLink(gameObject);
        }

        private void Awake()
        {
            if (txtSceneName != null && string.IsNullOrEmpty(txtSceneName.text))
                txtSceneName.text = "芦苇河 ▾";
            if (txtTimeOfDay != null && string.IsNullOrEmpty(txtTimeOfDay.text))
                txtTimeOfDay.text = "黄昏 · 晚霞";

            if (btnCurrencyPlus != null)
                btnCurrencyPlus.onClick.AddListener(() => ToastManager.Instance?.Show("庭贝充值功能开发中"));
            if (btnBaitPlus != null)
                btnBaitPlus.onClick.AddListener(() => UICommands.OpenShop());

            ApplyStyle(UIStyleService.Current);
        }

        /// <summary>Step11：从 DT_UIStyle 注入字号与尺寸（数据驱动，禁止硬编码）。</summary>
        public void ApplyStyle(UIStyleConfig s)
        {
            if (txtCurrency != null) txtCurrency.fontSize = s.topCountSize;
            if (txtBait != null) txtBait.fontSize = s.topCountSize;
            if (txtSceneName != null) txtSceneName.fontSize = s.sceneNameSize;
            if (txtTimeOfDay != null) txtTimeOfDay.fontSize = s.timeOfDaySize;
            if (btnCurrencyPlus != null)
                ((RectTransform)btnCurrencyPlus.transform).sizeDelta = new Vector2(s.topBarPlusSize, s.topBarPlusSize);
            if (btnBaitPlus != null)
                ((RectTransform)btnBaitPlus.transform).sizeDelta = new Vector2(s.topBarPlusSize, s.topBarPlusSize);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
