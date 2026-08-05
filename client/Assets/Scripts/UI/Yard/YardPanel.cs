using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WildCourt.Gameplay.Yard;
using WildCourt.Net;
using WildCourt.Net.Protocol;

namespace WildCourt.UI.Yard
{
    /// <summary>
    /// 庭院编辑面板（阶段 C，代码驱动 uGUI）：分类网格、长按拖放、旋转/删除、方案槽、保存、最小购买。
    /// 独立于 UIManager（其依赖预制体），美术资源到位后再接入。
    /// </summary>
    public class YardPanel : MonoBehaviour
    {
        public static YardPanel Instance { get; private set; }

        private const int Columns = 4;
        private const int PageSize = 12;

        private static readonly string[] Categories =
        {
            "全部", "景观", "家具", "容器", "建筑小品", "展示架", "宠物用品", "地面装饰", "节日限定",
        };

        private static readonly Color TabNormal = new Color(0.28f, 0.34f, 0.42f);
        private static readonly Color TabSelected = new Color(0.30f, 0.55f, 0.85f);

        private YardManager _yard;
        private Font _font;

        private readonly Dictionary<string, int> _owned = new();
        private readonly List<(Button Btn, Image Img, string Category)> _tabButtons = new();
        private readonly List<DecorationConfig> _visible = new();
        private int _shell;
        private int _page;
        private string _category = "全部";
        private int _selectedIndex = -1;

        private Text _shellText;
        private Text _hintText;
        private Text _pageText;
        private Transform _gridRoot;
        private GameObject _actionBar;
        private Button _prevButton;
        private Button _nextButton;

        public static YardPanel Create(YardManager yard)
        {
            var canvasGo = new GameObject("YardCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = canvasGo.AddComponent<YardPanel>();
            panel.Init(yard);
            return panel;
        }

        private void Init(YardManager yard)
        {
            Instance = this;
            _yard = yard;
            _font = LoadFont();

            if (YardRenderer.Instance != null)
            {
                YardRenderer.Instance.ItemSelected += OnItemSelected;
                YardRenderer.Instance.ItemDeselected += OnItemDeselected;
            }
            _yard.Error += OnYardError;

            BuildUI();
            RefreshList();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (YardRenderer.Instance != null)
            {
                YardRenderer.Instance.ItemSelected -= OnItemSelected;
                YardRenderer.Instance.ItemDeselected -= OnItemDeselected;
            }
            if (_yard != null) _yard.Error -= OnYardError;
        }

        // ---- 数据 ----

        public void OnProfileLoaded(UserProfileResponse data)
        {
            if (data == null) return;
            _shell = (int)data.shell;
            UpdateShellText();
        }

        public void OnInventoryLoaded(InventoryResponse data)
        {
            _owned.Clear();
            if (data != null && data.items != null)
            {
                foreach (var item in data.items)
                {
                    if (item != null) _owned[item.itemId] = item.count;
                }
            }
            RefreshList();
        }

        private void LoadInventory()
        {
            NetManager.Instance.Get<InventoryResponse>(ApiPaths.UserInventory,
                resp => OnInventoryLoaded(resp.data),
                (code, msg) => SetHint(msg));
        }

        private void BuyItem(DecorationConfig d)
        {
            NetManager.Instance.Post<BuyResponse>(ApiPaths.ShopBuy,
                new BuyRequest { itemId = d.id, count = 1, currency = "shell" },
                resp =>
                {
                    _shell = resp.data.shell;
                    UpdateShellText();
                    LoadInventory();
                },
                (code, msg) => SetHint(msg));
        }

        private void UpdateShellText()
        {
            if (_shellText != null) _shellText.text = "庭贝 " + _shell;
        }

        private void SetHint(string msg)
        {
            if (_hintText != null) _hintText.text = msg;
        }

        // ---- 交互回调（YardItemCell / YardRenderer） ----

        public void OnCellLongPress(DecorationConfig d)
        {
            if (_yard.State == YardManager.EditState.Idle)
            {
                _yard.BeginPlace(d.id);
                SetHint("拖到庭院里松手放置；右键/Esc 取消");
            }
        }

        public void OnCellRelease(DecorationConfig d, float downTime)
        {
            if (Time.unscaledTime - downTime < YardRenderer.LongPressSeconds &&
                _yard.State == YardManager.EditState.Idle)
            {
                // 短按：进入放置模式（点击后到庭院点击放置）
                _yard.BeginPlace(d.id);
                SetHint("点击庭院格子放置；右键/Esc 取消");
            }
        }

        private void OnItemSelected(int index)
        {
            _selectedIndex = index;
            if (_actionBar != null) _actionBar.SetActive(true);
            if (index >= 0 && index < _yard.Items.Count)
            {
                SetHint("已选中 " + _yard.Items[index].id + "，可旋转/删除或长按拖到新位置");
            }
        }

        private void OnItemDeselected()
        {
            _selectedIndex = -1;
            if (_actionBar != null) _actionBar.SetActive(false);
        }

        private void OnYardError(string msg)
        {
            SetHint(msg);
        }

        // ---- UI 构建 ----

        private void BuildUI()
        {
            var root = CreateUiObject("YardPanel", transform);
            SetAnchors(root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 320f));
            AddImage(root, new Color(0.16f, 0.20f, 0.26f, 0.96f));

            var header = CreateUiObject("Header", root.transform);
            SetAnchors(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -36f), new Vector2(-10f, -4f));
            _shellText = AddText(header, "庭贝 0", 20, new Color(1f, 0.85f, 0.3f));
            _shellText.alignment = TextAnchor.MiddleLeft;
            _hintText = AddText(header, "长按装饰拖到庭院；点击已摆放装饰可旋转/删除", 15, new Color(0.85f, 0.9f, 0.95f));
            _hintText.alignment = TextAnchor.MiddleRight;

            var tabRow = CreateUiObject("Tabs", root.transform);
            SetAnchors(tabRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -66f), new Vector2(-10f, -40f));
            var tabLayout = tabRow.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4f;
            tabLayout.childAlignment = TextAnchor.MiddleLeft;
            foreach (var cat in Categories)
            {
                var tabGo = CreateUiObject("tab_" + cat, tabRow.transform);
                var le = tabGo.AddComponent<LayoutElement>();
                le.preferredWidth = 96f;
                le.preferredHeight = 24f;
                var img = AddImage(tabGo, TabNormal);
                var btn = tabGo.AddComponent<Button>();
                btn.targetGraphic = img;
                var localCat = cat;
                btn.onClick.AddListener(() =>
                {
                    _category = localCat;
                    _page = 0;
                    RefreshList();
                });
                AddText(tabGo, cat, 15, Color.white);
                _tabButtons.Add((btn, img, cat));
            }

            var grid = CreateUiObject("Grid", root.transform);
            SetAnchors(grid, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -250f), new Vector2(-10f, -70f));
            _gridRoot = grid.transform;

            // 选中动作条（旋转/删除），默认隐藏
            _actionBar = CreateUiObject("Actions", root.transform);
            SetAnchors(_actionBar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 44f), new Vector2(-10f, 72f));
            var actionLayout = _actionBar.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 6f;
            actionLayout.childAlignment = TextAnchor.MiddleLeft;
            var rotateBtn = AddButton(CreateUiObject("Rotate", _actionBar.transform), "旋转", () =>
            {
                if (_selectedIndex >= 0) _yard.RotateItem(_selectedIndex);
            });
            SetButtonWidth(rotateBtn, 90f);
            var deleteBtn = AddButton(CreateUiObject("Delete", _actionBar.transform), "删除", () =>
            {
                if (_selectedIndex >= 0) _yard.RemoveItem(_selectedIndex);
            });
            SetButtonWidth(deleteBtn, 90f);
            _actionBar.SetActive(false);

            var footer = CreateUiObject("Footer", root.transform);
            SetAnchors(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 6f), new Vector2(-10f, 34f));
            var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 6f;
            footerLayout.childAlignment = TextAnchor.MiddleLeft;

            _prevButton = AddButton(CreateUiObject("Prev", footer.transform), "上一页", () => ChangePage(-1));
            SetButtonWidth(_prevButton, 90f);
            _pageText = AddText(CreateUiObject("Page", footer.transform), "1/1", 15, Color.white);
            var pageLe = _pageText.GetComponent<LayoutElement>() ?? _pageText.gameObject.AddComponent<LayoutElement>();
            pageLe.preferredWidth = 56f;
            _nextButton = AddButton(CreateUiObject("Next", footer.transform), "下一页", () => ChangePage(1));
            SetButtonWidth(_nextButton, 90f);

            for (int i = 1; i <= YardManager.LayoutSlotCount; i++)
            {
                int slot = i;
                var slotBtn = AddButton(CreateUiObject("Slot" + i, footer.transform), "方案" + i, () =>
                {
                    _yard.SwitchSlot(slot);
                    SetHint("当前方案槽 " + slot);
                });
                SetButtonWidth(slotBtn, 74f);
            }

            var saveBtn = AddButton(CreateUiObject("Save", footer.transform), "保存", () => _yard.SaveLayout());
            SetButtonWidth(saveBtn, 90f);
        }

        private void ChangePage(int delta)
        {
            _page += delta;
            RefreshList();
        }

        private void RefreshList()
        {
            _visible.Clear();
            var all = _yard.GetDecorations();
            foreach (var d in all)
            {
                if (_category == "全部" || d.category == _category) _visible.Add(d);
            }

            int pages = Mathf.Max(1, Mathf.CeilToInt(_visible.Count / (float)PageSize));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            if (_prevButton != null) _prevButton.interactable = _page > 0;
            if (_nextButton != null) _nextButton.interactable = _page < pages - 1;
            if (_pageText != null) _pageText.text = (_page + 1) + "/" + pages;

            BuildGrid();
            RefreshTabs();
        }

        private void BuildGrid()
        {
            if (_gridRoot == null) return;
            for (int i = _gridRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_gridRoot.GetChild(i).gameObject);
            }

            int start = _page * PageSize;
            for (int i = 0; i < PageSize && start + i < _visible.Count; i++)
            {
                CreateCell(_visible[start + i], i);
            }
        }

        private void CreateCell(DecorationConfig d, int index)
        {
            bool owned = _owned.TryGetValue(d.id, out var cnt) && cnt > 0;
            var cell = CreateUiObject("cell_" + d.id, _gridRoot);
            var rt = cell.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2((index % Columns) * 156f, -(index / Columns) * 58f);
            rt.sizeDelta = new Vector2(150f, 52f);
            AddImage(cell, owned ? YardRenderer.GetCategoryColor(d.category) : new Color(0.42f, 0.42f, 0.42f, 0.9f));

            var nameGo = CreateUiObject("name", cell.transform);
            SetStretch(nameGo);
            var nameText = AddText(nameGo, d.name, 15, Color.white);
            nameText.alignment = TextAnchor.UpperCenter;

            var priceGo = CreateUiObject("price", cell.transform);
            SetStretch(priceGo);
            var priceText = AddText(priceGo, owned ? "拥有 " + cnt : d.price + " 庭贝", 13, new Color(1f, 0.9f, 0.6f));
            priceText.alignment = TextAnchor.LowerCenter;

            if (owned)
            {
                cell.AddComponent<YardItemCell>().Init(this, d);
            }
            else
            {
                var buyGo = CreateUiObject("buy", cell.transform);
                var buyRt = buyGo.GetComponent<RectTransform>();
                buyRt.anchorMin = new Vector2(1f, 0f);
                buyRt.anchorMax = new Vector2(1f, 0f);
                buyRt.pivot = new Vector2(1f, 0f);
                buyRt.anchoredPosition = new Vector2(-3f, 3f);
                buyRt.sizeDelta = new Vector2(54f, 22f);
                AddButton(buyGo, "购买", () => BuyItem(d));
            }
        }

        private void RefreshTabs()
        {
            foreach (var tab in _tabButtons)
            {
                tab.Img.color = tab.Category == _category ? TabSelected : TabNormal;
            }
        }

        // ---- 通用 UI 工具 ----

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return go;
        }

        private static void SetAnchors(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static void SetStretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private Image AddImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private Text AddText(GameObject go, string content, int size, Color color)
        {
            var text = go.AddComponent<Text>();
            text.text = content;
            if (_font != null) text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private Button AddButton(GameObject go, string label, Action onClick)
        {
            var img = AddImage(go, new Color(0.28f, 0.48f, 0.78f));
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var labelGo = CreateUiObject("label", go.transform);
            SetStretch(labelGo);
            AddText(labelGo, label, 16, Color.white);
            return btn;
        }

        private static void SetButtonWidth(Button btn, float width)
        {
            var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 26f;
        }

        private static Font LoadFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }

    /// <summary>
    /// 装饰格子：长按触发拖放（阶段 C），短按进入放置模式。
    /// </summary>
    public class YardItemCell : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private YardPanel _panel;
        private DecorationConfig _config;
        private bool _pressed;
        private float _downTime;

        public void Init(YardPanel panel, DecorationConfig config)
        {
            _panel = panel;
            _config = config;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            _downTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            if (_panel != null) _panel.OnCellRelease(_config, _downTime);
        }

        private void Update()
        {
            if (_pressed && _panel != null && Time.unscaledTime - _downTime >= YardRenderer.LongPressSeconds)
            {
                _pressed = false;
                _panel.OnCellLongPress(_config);
            }
        }
    }
}
