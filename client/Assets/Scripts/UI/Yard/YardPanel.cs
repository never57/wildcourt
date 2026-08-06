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
        private GameObject _panelRoot;
        private GameObject _expandButton;
        private PlayerInfoEntry _infoEntry;
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
            panel._infoEntry = PlayerInfoEntry.Create(canvasGo.transform, null);
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
            _yard.LayoutChanged += OnLayoutChanged;

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
            if (_yard != null) _yard.LayoutChanged -= OnLayoutChanged;
        }

        // ---- 数据 ----

        public void OnProfileLoaded(UserProfileResponse data)
        {
            if (data == null) return;
            _shell = (int)data.shell;
            UpdateShellText();
            if (_infoEntry != null) _infoEntry.UpdateProfile(data);
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
            // 短按仅提示：放置走长按拖放
            if (Time.unscaledTime - downTime < YardRenderer.LongPressSeconds)
            {
                SetHint("长按图标拖到庭院放置；点击已摆放装饰可旋转/镜像/删除");
            }
        }

        private void OnItemSelected(int index)
        {
            _selectedIndex = index;
            if (_actionBar != null) _actionBar.SetActive(true);
            if (index >= 0 && index < _yard.Items.Count)
            {
                SetHint("已选中 " + _yard.Items[index].id + "，可旋转/镜像/删除或长按拖到新位置");
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

        private void OnLayoutChanged()
        {
            // 摆放/移动/旋转/切换槽后刷新“已建造”数量
            RefreshList();
        }

        // ---- UI 构建 ----

        private void BuildUI()
        {
            var root = CreateUiObject("YardPanel", transform);
            _panelRoot = root;
            SetAnchors(root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 248f));
            AddImage(root, new Color(0.16f, 0.20f, 0.26f, 0.96f));

            var header = CreateUiObject("Header", root.transform);
            SetAnchors(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -30f), new Vector2(-10f, -4f));
            var shellGo = CreateUiObject("Shell", header.transform);
            SetStretch(shellGo);
            _shellText = AddText(shellGo, "庭贝 0", 16, new Color(1f, 0.85f, 0.3f));
            _shellText.alignment = TextAnchor.MiddleLeft;
            var hintGo = CreateUiObject("Hint", header.transform);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = Vector2.zero;
            hintRt.anchorMax = Vector2.one;
            hintRt.offsetMin = Vector2.zero;
            hintRt.offsetMax = new Vector2(-68f, 0f);
            _hintText = AddText(hintGo, "长按图标拖到庭院放置；点击已摆放装饰可旋转/镜像/删除", 13, new Color(0.85f, 0.9f, 0.95f));
            _hintText.alignment = TextAnchor.MiddleRight;

            var collapseGo = CreateUiObject("Collapse", header.transform);
            var collapseRt = collapseGo.GetComponent<RectTransform>();
            collapseRt.anchorMin = new Vector2(1f, 0f);
            collapseRt.anchorMax = new Vector2(1f, 1f);
            collapseRt.pivot = new Vector2(1f, 0.5f);
            collapseRt.anchoredPosition = new Vector2(-4f, 0f);
            collapseRt.sizeDelta = new Vector2(60f, 20f);
            AddButton(collapseGo, "收起", () => TogglePanel(false));

            var tabRow = CreateUiObject("Tabs", root.transform);
            SetAnchors(tabRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -56f), new Vector2(-10f, -34f));
            var tabLayout = tabRow.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4f;
            tabLayout.childAlignment = TextAnchor.MiddleLeft;
            foreach (var cat in Categories)
            {
                var tabGo = CreateUiObject("tab_" + cat, tabRow.transform);
                var le = tabGo.AddComponent<LayoutElement>();
                le.preferredWidth = 96f;
                le.preferredHeight = 20f;
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
                var tabLabelGo = CreateUiObject("label", tabGo.transform);
                SetStretch(tabLabelGo);
                AddText(tabLabelGo, cat, 13, Color.white);
                _tabButtons.Add((btn, img, cat));
            }

            var grid = CreateUiObject("Grid", root.transform);
            SetAnchors(grid, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -216f), new Vector2(-10f, -60f));
            var gridLayout = grid.AddComponent<GridLayoutGroup>();
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Columns;
            gridLayout.cellSize = new Vector2(118f, 44f);
            gridLayout.spacing = new Vector2(8f, 8f);
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            gridLayout.padding = new RectOffset(6, 6, 4, 4);
            _gridRoot = grid.transform;

            // 选中动作条（旋转/镜像/删除），默认隐藏
            _actionBar = CreateUiObject("Actions", root.transform);
            SetAnchors(_actionBar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 38f), new Vector2(-10f, 60f));
            var actionLayout = _actionBar.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 6f;
            actionLayout.childAlignment = TextAnchor.MiddleLeft;
            var rotateBtn = AddButton(CreateUiObject("Rotate", _actionBar.transform), "旋转", () =>
            {
                if (_selectedIndex >= 0) _yard.RotateItem(_selectedIndex);
            });
            SetButtonWidth(rotateBtn, 80f);
            var mirrorBtn = AddButton(CreateUiObject("Mirror", _actionBar.transform), "镜像", () =>
            {
                if (_selectedIndex >= 0) _yard.MirrorItem(_selectedIndex);
            });
            SetButtonWidth(mirrorBtn, 80f);
            var deleteBtn = AddButton(CreateUiObject("Delete", _actionBar.transform), "删除", () =>
            {
                if (_selectedIndex >= 0) _yard.RemoveItem(_selectedIndex);
            });
            SetButtonWidth(deleteBtn, 80f);
            _actionBar.SetActive(false);

            var footer = CreateUiObject("Footer", root.transform);
            SetAnchors(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 4f), new Vector2(-10f, 28f));
            var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 6f;
            footerLayout.childAlignment = TextAnchor.MiddleLeft;

            _prevButton = AddButton(CreateUiObject("Prev", footer.transform), "上一页", () => ChangePage(-1));
            SetButtonWidth(_prevButton, 80f);
            _pageText = AddText(CreateUiObject("Page", footer.transform), "1/1", 13, Color.white);
            var pageLe = _pageText.GetComponent<LayoutElement>() ?? _pageText.gameObject.AddComponent<LayoutElement>();
            pageLe.preferredWidth = 48f;
            _nextButton = AddButton(CreateUiObject("Next", footer.transform), "下一页", () => ChangePage(1));
            SetButtonWidth(_nextButton, 80f);

            for (int i = 1; i <= YardManager.LayoutSlotCount; i++)
            {
                int slot = i;
                var slotBtn = AddButton(CreateUiObject("Slot" + i, footer.transform), "方案" + i, () =>
                {
                    _yard.SwitchSlot(slot);
                    SetHint("当前方案槽 " + slot);
                });
                SetButtonWidth(slotBtn, 64f);
            }

            var saveBtn = AddButton(CreateUiObject("Save", footer.transform), "保存", () => _yard.SaveLayout());
            SetButtonWidth(saveBtn, 80f);

            // 展开按钮（收起后常驻左下角）
            _expandButton = CreateUiObject("Expand", transform);
            SetAnchors(_expandButton, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 10f), new Vector2(80f, 36f));
            AddButton(_expandButton, "展开", () => TogglePanel(true));
            _expandButton.SetActive(false);

            // 缩放按钮（右上角，不受面板收起影响）
            var zoomOut = CreateUiObject("ZoomOut", transform);
            SetAnchors(zoomOut, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-92f, -12f), new Vector2(-62f, -42f));
            AddButton(zoomOut, "－", () => Zoom(-1));
            var zoomIn = CreateUiObject("ZoomIn", transform);
            SetAnchors(zoomIn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-56f, -12f), new Vector2(-26f, -42f));
            AddButton(zoomIn, "＋", () => Zoom(1));
        }

        private void TogglePanel(bool show)
        {
            if (_panelRoot != null) _panelRoot.SetActive(show);
            if (_expandButton != null) _expandButton.SetActive(!show);
        }

        private void Zoom(int dir)
        {
            if (YardRenderer.Instance != null)
            {
                YardRenderer.Instance.ZoomBy(dir * YardRenderer.ZoomStep);
            }
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
                CreateCell(_visible[start + i]);
            }
        }

        private void CreateCell(DecorationConfig d)
        {
            int owned = _owned.TryGetValue(d.id, out var cnt) ? cnt : 0;
            int built = CountBuilt(d.id);
            int remain = d.maxCount - owned;
            bool canBuy = remain > 0;

            var cell = CreateUiObject("cell_" + d.id, _gridRoot);
            var img = AddImage(cell, owned > 0 ? YardRenderer.GetCategoryColor(d.category) : new Color(0.42f, 0.42f, 0.42f, 0.9f));
            // 商品图异步加载（StreamingAssets/ui/Decorations/{id}.png）：有图换图，无图保持色块
            GoodsSprites.Request(d.id, (loadedId, sprite) =>
            {
                if (sprite == null || img == null) return;
                img.sprite = sprite;
                img.color = owned > 0 ? Color.white : new Color(0.62f, 0.62f, 0.62f, 0.9f);
            });

            // 顶部：名称
            var nameGo = CreateUiObject("name", cell.transform);
            SetStretch(nameGo);
            var nameText = AddText(nameGo, d.name, 12, Color.white);
            nameText.alignment = TextAnchor.UpperCenter;

            // 中间：已建造 X / 拥有 Y
            var countGo = CreateUiObject("count", cell.transform);
            SetStretch(countGo);
            var countText = AddText(countGo, $"已建造 {built}/{owned}", 11, new Color(1f, 0.9f, 0.6f));
            countText.alignment = TextAnchor.MiddleCenter;

            // 右下角：剩余 N / 已满
            var remainGo = CreateUiObject("remain", cell.transform);
            var remainRt = remainGo.GetComponent<RectTransform>();
            remainRt.anchorMin = new Vector2(1f, 0f);
            remainRt.anchorMax = new Vector2(1f, 0f);
            remainRt.pivot = new Vector2(1f, 0f);
            remainRt.anchoredPosition = new Vector2(-4f, 3f);
            remainRt.sizeDelta = new Vector2(56f, 18f);
            var remainText = AddText(remainGo, canBuy ? $"剩余 {remain}" : "已满", 10, new Color(0.85f, 0.85f, 0.9f));
            remainText.alignment = TextAnchor.MiddleRight;

            // 左下：购买按钮（剩余 > 0 时）
            if (canBuy)
            {
                var buyGo = CreateUiObject("buy", cell.transform);
                var buyRt = buyGo.GetComponent<RectTransform>();
                buyRt.anchorMin = new Vector2(0f, 0f);
                buyRt.anchorMax = new Vector2(0f, 0f);
                buyRt.pivot = new Vector2(0f, 0f);
                buyRt.anchoredPosition = new Vector2(4f, 3f);
                buyRt.sizeDelta = new Vector2(44f, 18f);
                AddButton(buyGo, "购买", () => BuyItem(d));
            }

            if (owned > 0)
            {
                cell.AddComponent<YardItemCell>().Init(this, d);
            }
        }

        /// <summary>当前方案槽中已摆放的同 ID 装饰数量。</summary>
        private int CountBuilt(string id)
        {
            int count = 0;
            foreach (var item in _yard.Items)
            {
                if (item.id == id) count++;
            }
            return count;
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
            // uGUI 限制：一个 GameObject 只能有一个 Graphic（Image/Text/RawImage 互斥），
            // 已有同类型或添加失败时挂到子节点，避免 AddComponent 返回 null。
            if (go.GetComponent<Text>() != null)
            {
                var child = CreateUiObject("text", go.transform);
                SetStretch(child);
                return AddText(child, content, size, color);
            }
            var text = go.AddComponent<Text>();
            if (text == null)
            {
                var child = CreateUiObject("text", go.transform);
                SetStretch(child);
                return AddText(child, content, size, color);
            }
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
            AddText(labelGo, label, 14, Color.white);
            return btn;
        }

        private static void SetButtonWidth(Button btn, float width)
        {
            var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 22f;
        }

        private static Font LoadFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }

    /// <summary>
    /// 装饰格子：长按触发拖放放置（阈值见 YardRenderer.LongPressSeconds），短按仅提示。
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
