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
    /// 庭院编辑面板：分类网格、长按拖放、旋转/删除、方案槽、保存、最小购买。
    /// UI 结构由 YardPanel 预制体提供；预制体缺失/损坏时视为资源损坏，退出游戏待热更修复。
    /// </summary>
    public class YardPanel : MonoBehaviour
    {
        public static YardPanel Instance { get; private set; }

        /// <summary>
        /// 跨场景预加载的 YardPanel 预制体引用（对应 VideoSceneTransition.PreloadedLoginUI）。
        /// 登录成功、切 MainScene 前由 LoginController 注入；支持热更（热更包下载的 prefab 赋值即可）。
        /// </summary>
        public static GameObject YardPanelPrefab { get; set; }

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

        [Header("预制体绑定（YardPanel.prefab 内字段）")]
        [SerializeField] private Text _shellText;
        [SerializeField] private Text _hintText;
        [SerializeField] private Text _pageText;
        [SerializeField] private Transform _gridRoot;
        [SerializeField] private GameObject _cellPrefab;
        [SerializeField] private GameObject _actionBar;
        [SerializeField] private Button _rotateButton;
        [SerializeField] private Button _mirrorButton;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private GameObject _expandButton;
        [SerializeField] private Button _collapseButton;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button[] _slotButtons;
        [SerializeField] private Button _zoomOutButton;
        [SerializeField] private Button _zoomInButton;
        [SerializeField] private Button[] _tabButtonsArray;
        [SerializeField] private Image[] _tabImagesArray;
        private PlayerInfoEntry _infoEntry;

        /// <summary>
        /// 入口：实例化 YardPanel 预制体并初始化。预制体缺失或未挂 YardPanel 脚本时，
        /// 说明资源损坏/热更未到位，直接退出游戏（玩家重新进入时走热更修复）。
        /// </summary>
        public static YardPanel CreateOrInstantiate(YardManager yard)
        {
            var prefab = LoadYardPanelPrefab();
            if (prefab != null)
            {
                var go = Instantiate(prefab);
                var panel = go.GetComponent<YardPanel>();
                if (panel != null)
                {
                    panel.InitFromPrefab(yard);
                    return panel;
                }
                Destroy(go);
            }
            QuitForMissingAsset();
            return null;
        }

        /// <summary>
        /// YardPanel 预制体缺失/损坏（找不到引用或未挂 YardPanel 脚本）：
        /// 记录错误并退出游戏，玩家重新进入即可触发热更新修复资源。
        /// </summary>
        private static void QuitForMissingAsset()
        {
            Debug.LogError("[YardPanel] YardPanel 预制体丢失或损坏，无法构建庭院面板，请重新进入游戏触发热更新。");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// 加载 YardPanel 预制体：仅使用登录阶段注入的 YardPanelPrefab 引用（支持热更）。
        /// 未注入返回 null，调用方视为资源损坏并退出游戏。
        /// </summary>
        private static GameObject LoadYardPanelPrefab()
        {
            return YardPanelPrefab;
        }

        /// <summary>预制体路径初始化：UI 结构已在预制体中搭好，这里只做数据绑定与事件接线。</summary>
        private void InitFromPrefab(YardManager yard)
        {
            Instance = this;
            _yard = yard;
            _font = LoadFont();

            SubscribeCore();

            if (_tabButtonsArray != null && _tabImagesArray != null)
            {
                _tabButtons.Clear();
                for (int i = 0; i < _tabButtonsArray.Length && i < Categories.Length; i++)
                {
                    var cat = Categories[i];
                    var b = _tabButtonsArray[i];
                    if (b == null) continue;
                    // 图片未单独绑定时回退按钮的 targetGraphic，避免 RefreshTabs 空引用
                    var im = i < _tabImagesArray.Length ? _tabImagesArray[i] : null;
                    im ??= b.targetGraphic as Image;
                    b.onClick.AddListener(() =>
                    {
                        _category = cat;
                        _page = 0;
                        RefreshList();
                    });
                    _tabButtons.Add((b, im, cat));
                }
            }

            _prevButton?.onClick.AddListener(() => ChangePage(-1));
            _nextButton?.onClick.AddListener(() => ChangePage(1));
            _saveButton?.onClick.AddListener(() => _yard.SaveLayout());
            if (_slotButtons != null)
            {
                for (int i = 0; i < _slotButtons.Length; i++)
                {
                    int slot = i + 1;
                    var b = _slotButtons[i];
                    b.onClick.AddListener(() =>
                    {
                        _yard.SwitchSlot(slot);
                        SetHint("当前方案槽 " + slot);
                    });
                }
            }
            _rotateButton?.onClick.AddListener(() =>
            {
                if (_selectedIndex >= 0) _yard.RotateItem(_selectedIndex);
            });
            _mirrorButton?.onClick.AddListener(() =>
            {
                if (_selectedIndex >= 0) _yard.MirrorItem(_selectedIndex);
            });
            _deleteButton?.onClick.AddListener(() =>
            {
                if (_selectedIndex >= 0) _yard.RemoveItem(_selectedIndex);
            });
            _collapseButton?.onClick.AddListener(() => TogglePanel(false));
            if (_expandButton != null)
            {
                _expandButton.GetComponent<Button>()?.onClick.AddListener(() => TogglePanel(true));
                _expandButton.SetActive(false);
            }
            _zoomOutButton?.onClick.AddListener(() => Zoom(-1));
            _zoomInButton?.onClick.AddListener(() => Zoom(1));

            LoadInventory();
            RefreshList();
        }
        private void SubscribeCore()
        {
            if (YardRenderer.Instance != null)
            {
                YardRenderer.Instance.ItemSelected += OnItemSelected;
                YardRenderer.Instance.ItemDeselected += OnItemDeselected;
            }
            _yard.Error += OnYardError;
            _yard.LayoutChanged += OnLayoutChanged;
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
            if (_yard != null) _yard.SetOwned(_owned);
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
            // 预制体路径：格子模板 Instantiate + YardCell 绑定
            if (_cellPrefab != null && _gridRoot != null)
            {
                var cellGo = Instantiate(_cellPrefab, _gridRoot);
                var cellComp = cellGo.GetComponent<YardCell>();
                if (cellComp != null)
                {
                    int prefOwned = _owned.TryGetValue(d.id, out var prefCnt) ? prefCnt : 0;
                    int prefBuilt = CountBuilt(d.id);
                    int prefRemain = d.maxCount - prefOwned;
                    bool ownedFlag = prefOwned > 0;
                    cellComp.Bind(d, prefOwned, prefBuilt, prefRemain,
                        () => BuyItem(d),
                        ownedFlag ? YardRenderer.GetCategoryColor(d.category) : new Color(0.42f, 0.42f, 0.42f, 0.9f),
                        ownedFlag);
                    return;
                }
                Destroy(cellGo);
            }

            // —— 回退：代码建格 ——
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
                if (tab.Img == null) continue;
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
