using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Game.Data;
using Game.Services;

namespace Game.UI
{
    /// <summary>
    /// 鱼饵商店面板：商品列表数据驱动、数量选择、实时总价、购买双重校验、顶栏联动。
    /// </summary>
    public class ShopPanelController : MonoBehaviour
    {
        [SerializeField] GameObject rowPrefab;
        [SerializeField] Transform content;
        [SerializeField] Image imgMask;
        [SerializeField] Text txtCurrency;
        [SerializeField] Button btnClose;
        [SerializeField] Text txtEmpty;

        private FishingDataTable _table;
        private readonly List<ShopItemRowController> _rows = new List<ShopItemRowController>();
        private readonly Stack<ShopItemRowController> _pool = new Stack<ShopItemRowController>();
        private bool _subscribed;
        private bool _open;

        public bool IsOpen => _open;
        public IReadOnlyList<ShopItemRowController> Rows => _rows;

        public void Init(FishingDataTable dt, GameObject rowPrefabAsset, Transform contentTrans)
        {
            _table = dt;
            rowPrefab = rowPrefabAsset;
            content = contentTrans;

            ApplyStyle(UIStyleService.Current);

            if (!_subscribed)
            {
                _subscribed = true;
                // 注意：实例方法名不可与静态 UICommands.OpenShop 相同
                UICommands.OnOpenShop += HandleOpenShop;
            }

            if (btnClose != null) btnClose.onClick.AddListener(Close);
            if (imgMask != null)
            {
                var maskBtn = imgMask.GetComponent<Button>();
                if (maskBtn == null) maskBtn = imgMask.gameObject.AddComponent<Button>();
                maskBtn.onClick.AddListener(Close);
            }
        }

        private void HandleOpenShop()
        {
            Open();
        }

        private void OnDestroy()
        {
            if (_subscribed)
            {
                UICommands.OnOpenShop -= HandleOpenShop;
                _subscribed = false;
            }
        }

        private void Open()
        {
            _open = true;
            gameObject.SetActive(true);

            transform.DOKill();
            transform.localScale = Vector3.one * 0.9f;
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.15f).SetLink(gameObject);
            transform.DOScale(Vector3.one, 0.15f).SetLink(gameObject);

            // 打开期间订阅货币/物品事件实时刷新
            InventoryService.Instance.OnCurrencyChanged += OnCurrencyChanged;
            InventoryService.Instance.OnItemCountChanged += OnItemCountChanged;

            BuildRows();
            RefreshHeader();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;

            InventoryService.Instance.OnCurrencyChanged -= OnCurrencyChanged;
            InventoryService.Instance.OnItemCountChanged -= OnItemCountChanged;

            transform.DOKill();
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.DOFade(0f, 0.15f).OnComplete(() => gameObject.SetActive(false)).SetLink(gameObject);
            transform.DOScale(Vector3.one * 0.9f, 0.15f).SetLink(gameObject);
        }

        private void OnCurrencyChanged(int v) => RefreshHeader();
        private void OnItemCountChanged(string id, int c) => RefreshAllRows();

        private void RefreshHeader()
        {
            if (txtCurrency != null) txtCurrency.text = InventoryService.Instance.Currency.ToString();
        }

        private void RefreshAllRows()
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].RefreshBuyState();
        }

        /// <summary>Step11：从 DT_UIStyle 注入面板字号与关闭按钮尺寸（数据驱动，禁止硬编码）。</summary>
        public void ApplyStyle(UIStyleConfig s)
        {
            if (txtCurrency != null) txtCurrency.fontSize = s.topCountSize;
            if (txtEmpty != null) txtEmpty.fontSize = s.descSize;
            if (btnClose != null) CloseButtonHelper.Apply(btnClose, s);
        }

        private void BuildRows()
        {
            if (_table == null || rowPrefab == null) return;

            // 回收旧行
            for (int i = 0; i < _rows.Count; i++) _pool.Push(_rows[i]);
            _rows.Clear();

            var baits = _table.baits;
            if (baits == null || baits.Length == 0)
            {
                if (txtEmpty != null) txtEmpty.gameObject.SetActive(true);
                return;
            }
            if (txtEmpty != null) txtEmpty.gameObject.SetActive(false);

            foreach (var bait in baits)
            {
                ShopItemRowController row;
                if (_pool.Count > 0)
                {
                    row = _pool.Pop();
                    row.gameObject.SetActive(true);
                    row.transform.SetAsLastSibling();
                }
                else
                {
                    var go = Instantiate(rowPrefab, content);
                    row = go.GetComponent<ShopItemRowController>();
                    if (row == null) row = go.AddComponent<ShopItemRowController>();
                }
                row.Bind(bait);
                _rows.Add(row);
            }
        }
    }
}
