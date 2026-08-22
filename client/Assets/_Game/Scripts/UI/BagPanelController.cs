using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Game.Data;
using Game.Services;

namespace Game.UI
{
    /// <summary>
    /// 背包面板：鱼饵/鱼双 Tab、行项展示、出售换货币、设为当前鱼饵。
    /// </summary>
    public class BagPanelController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] Image imgMask;
        [SerializeField] RectTransform panelBody;
        [SerializeField] Button btnClose;
        [SerializeField] Button btnTabBait;
        [SerializeField] Button btnTabFish;
        [SerializeField] Image imgTabBaitBg;
        [SerializeField] Image imgTabFishBg;
        [SerializeField] Text txtTabBait;
        [SerializeField] Text txtTabFish;
        [SerializeField] ScrollRect scrollItems;
        [SerializeField] RectTransform contentRoot;
        [SerializeField] Text txtEmpty;
        [SerializeField] GameObject rowPrefab;     // PF_InventoryItemRow
        [SerializeField] FishingDataTable table;   // DT_Fishing

        private readonly List<InventoryItemRowController> _rows = new List<InventoryItemRowController>();
        private bool _baitTab = true;

        public enum PanelTab { Bait, Fish }

        public bool IsOpen => gameObject.activeInHierarchy;
        public PanelTab CurrentTab => _baitTab ? PanelTab.Bait : PanelTab.Fish;
        public IReadOnlyList<InventoryItemRowController> Rows => _rows;
        public Text EmptyText => txtEmpty;
        public Button MaskButton => imgMask != null ? imgMask.GetComponent<Button>() : null;

        public void SwitchToTab(bool bait) => SetTab(bait);

        /// <summary>由 Bootstrap 注入数据表与行预制件，并在实例化后订阅命令（prefab 默认 inactive，Awake 不触发）。</summary>
        public void Init(FishingDataTable dt, GameObject rowPrefabAsset)
        {
            table = dt;
            rowPrefab = rowPrefabAsset;

            ApplyStyle(UIStyleService.Current);

            if (!_subscribed)
            {
                _subscribed = true;
                // 注意：实例方法名不可与静态 UICommands.OpenBag/OpenCreel 相同，否则会被解析为静态方法
                UICommands.OnOpenBag += HandleOpenBag;
                UICommands.OnOpenCreel += HandleOpenCreel;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed)
            {
                UICommands.OnOpenBag -= HandleOpenBag;
                UICommands.OnOpenCreel -= HandleOpenCreel;
                _subscribed = false;
            }
        }

        private bool _subscribed;

        private void HandleOpenBag()
        {
            _baitTab = true;
            Open();
        }

        private void HandleOpenCreel()
        {
            _baitTab = false;
            Open();
        }

        private void Start()
        {
            btnClose.onClick.AddListener(() => Close());
            imgMask.raycastTarget = true;
            // Mask 点击关闭（Image 无 Button 时用 EventTrigger，这里用 Button 承载）
            var maskBtn = imgMask.gameObject.GetComponent<Button>();
            if (maskBtn == null) maskBtn = imgMask.gameObject.AddComponent<Button>();
            maskBtn.onClick.AddListener(() => Close());

            btnTabBait.onClick.AddListener(() => SetTab(true));
            btnTabFish.onClick.AddListener(() => SetTab(false));
        }

        private void Open()
        {
            gameObject.SetActive(true);
            SubscribeInventory();

            // 打开动画：DOScale(0.9→1) + DOFade(0→1) 0.15s
            panelBody.localScale = Vector3.one * 0.9f;
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
            panelBody.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack).SetLink(gameObject);
            if (cg != null) cg.DOFade(1f, 0.15f).SetLink(gameObject);

            Refresh();
        }

        public void Close()
        {
            UnsubscribeInventory();
            panelBody.DOScale(Vector3.one * 0.9f, 0.15f).SetLink(gameObject);
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.DOFade(0f, 0.15f).OnComplete(() => gameObject.SetActive(false)).SetLink(gameObject);
            else gameObject.SetActive(false);
        }

        private void SubscribeInventory()
        {
            InventoryService.Instance.OnItemCountChanged += OnInventoryChanged;
            InventoryService.Instance.OnSelectedBaitChanged += OnInventoryChanged;
            InventoryService.Instance.OnCurrencyChanged += OnInventoryChanged;
        }

        private void UnsubscribeInventory()
        {
            InventoryService.Instance.OnItemCountChanged -= OnInventoryChanged;
            InventoryService.Instance.OnSelectedBaitChanged -= OnInventoryChanged;
            InventoryService.Instance.OnCurrencyChanged -= OnInventoryChanged;
        }

        private void OnInventoryChanged(string id, int count) => Refresh();
        private void OnInventoryChanged(BaitConfig b) => Refresh();
        private void OnInventoryChanged(int c) => Refresh();

        private void SetTab(bool bait)
        {
            if (_baitTab == bait) return;
            _baitTab = bait;
            Refresh();
        }

        /// <summary>Step11：从 DT_UIStyle 注入面板尺寸与字号（数据驱动，禁止硬编码）。</summary>
        public void ApplyStyle(UIStyleConfig s)
        {
            if (panelBody != null) panelBody.sizeDelta = s.panelBodySize;
            if (btnClose != null) CloseButtonHelper.Apply(btnClose, s);
            if (txtTabBait != null) txtTabBait.fontSize = s.bodySize;
            if (txtTabFish != null) txtTabFish.fontSize = s.bodySize;
            if (txtEmpty != null) txtEmpty.fontSize = s.descSize;
        }

        public void Refresh()
        {
            if (!gameObject.activeInHierarchy) return;

            // 更新 Tab 视觉
            SetTabVisual(imgTabBaitBg, txtTabBait, _baitTab);
            SetTabVisual(imgTabFishBg, txtTabFish, !_baitTab);

            // 清空旧行（对象池复用）
            foreach (var row in _rows) row.gameObject.SetActive(false);

            int used = 0;
            bool any = false;
            var inv = InventoryService.Instance;

            if (_baitTab)
            {
                foreach (var bait in table.baits)
                {
                    if (bait == null) continue;
                    int count = inv.GetCount(bait.id);
                    if (count <= 0) continue;
                    any = true;
                    var row = GetRow(used++);
                    row.BindBait(bait, count, inv.SelectedBait != null && inv.SelectedBait.id == bait.id);
                    row.OnAction = HandleBaitAction;
                    row.gameObject.SetActive(true);
                }
                txtEmpty.text = "没有鱼饵，去商店购买";
            }
            else
            {
                foreach (var fish in table.fishes)
                {
                    if (fish == null) continue;
                    int count = inv.GetCount(fish.id);
                    if (count <= 0) continue;
                    any = true;
                    var row = GetRow(used++);
                    row.BindFish(fish, count);
                    row.OnAction = HandleFishAction;
                    row.gameObject.SetActive(true);
                }
                txtEmpty.text = "还没有鱼，去钓一条吧！";
            }

            txtEmpty.gameObject.SetActive(!any);
        }

        private void SetTabVisual(Image bg, Text txt, bool selected)
        {
            if (bg != null) bg.sprite = LoadSprite(selected ? "PH_Tab_Selected" : "PH_Tab_Normal");
            if (txt != null) txt.color = selected ? new Color(0.1f, 0.15f, 0.1f, 1f) : new Color(0.45f, 0.45f, 0.48f, 1f);
        }

        private InventoryItemRowController GetRow(int index)
        {
            while (_rows.Count <= index)
            {
                var go = Instantiate(rowPrefab, contentRoot);
                go.SetActive(false);
                _rows.Add(go.GetComponent<InventoryItemRowController>());
            }
            var row = _rows[index];
            row.transform.SetAsLastSibling();
            return row;
        }

        private void HandleBaitAction(InventoryItemRowController row, InventoryItemRowController.RowAction action)
        {
            if (action == InventoryItemRowController.RowAction.SelectBait)
            {
                if (row.Bait != null && InventoryService.Instance.TrySelectBait(row.Bait.id))
                {
                    ToastManager.Instance?.Show($"已装备【{row.Bait.displayName}】");
                    Refresh();
                }
            }
        }

        private void HandleFishAction(InventoryItemRowController row, InventoryItemRowController.RowAction action)
        {
            switch (action)
            {
                case InventoryItemRowController.RowAction.Use:
                    ToastManager.Instance?.Show("加工材料，加工系统未开放（预留）");
                    break;
                case InventoryItemRowController.RowAction.Sell:
                    if (row.Fish != null)
                    {
                        var fish = row.Fish;
                        ConfirmDialog.Show($"确认出售【{fish.displayName}】×1？\n可获得 {fish.sellPrice} 庭贝", () =>
                        {
                            if (InventoryService.Instance.TryRemoveItem(fish.id, 1))
                            {
                                InventoryService.Instance.AddCurrency(fish.sellPrice);
                                ToastManager.Instance?.Show($"出售【{fish.displayName}】×1 +{fish.sellPrice} 庭贝");
                                Refresh();
                            }
                        });
                    }
                    break;
            }
        }

        private static Sprite LoadSprite(string name)
        {
            // 构建脚本直接绑定 sprite 引用
            return null;
        }
    }
}
