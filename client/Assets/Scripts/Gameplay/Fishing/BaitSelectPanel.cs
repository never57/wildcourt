using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WildCourt.Core;

namespace WildCourt.Gameplay.Fishing
{
    /// <summary>
    /// 背包鱼饵选择面板（业务流程第一步：背包选鱼饵 → 跳转钓鱼场景）。
    /// <para>纯代码搭建，不依赖预制体；可由任意按钮调用 <see cref="Show"/> 打开。</para>
    /// <para>选中鱼饵后写入 <see cref="FishingLoadout"/> 并加载 FishingScene，
    /// 钓鱼场景引导会自动读取并开始垂钓。本面板不做鱼竿选择（项目无鱼竿逻辑）。</para>
    /// </summary>
    public static class BaitSelectPanel
    {
        // ---- 数据来源（外部注入；未注入时用测试实例，保证开箱可运行）----
        private static InventoryManager _inv;
        private static CurrencyManager _cur;
        private static PlayerLevel _pLv;
        private static FishingConfigSO _config;
        private static GameObject _panelRoot;
        private static Text _hintText;
        private static bool _built;

        /// <summary>
        /// 注入背包/货币/等级实例（由登录/庭院加载处在打开面板前调用一次）。
        /// 未调用时面板自动创建测试实例，保证独立可运行。
        /// </summary>
        public static void Configure(InventoryManager inv, CurrencyManager cur, PlayerLevel pLv)
        {
            _inv = inv;
            _cur = cur;
            _pLv = pLv;
        }

        /// <summary>打开鱼饵选择面板（可绑定到任意"钓鱼"按钮的 onClick）。</summary>
        public static void Show()
        {
            EnsureInstances();
            EnsureConfig();
            EnsureBuilt();
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
                RefreshCounts();
            }
        }

        /// <summary>关闭面板（不跳转场景）。</summary>
        public static void Hide()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        // ================= 内部 =================

        private static void EnsureInstances()
        {
            // 优先复用 FishingLoadout 已注入的实例（从真实业务流带来）
            if (_inv == null && FishingLoadout.HasLoadout)
            {
                _inv = FishingLoadout.Inventory;
                _cur = FishingLoadout.Currency;
                _pLv = FishingLoadout.Player;
            }
            // 独立测试：自建实例 + 测试数据
            if (_inv == null)
            {
                _inv = new InventoryManager();
                _cur = new CurrencyManager();
                _pLv = new PlayerLevel();
                _pLv.Init();
                _pLv.SetExp(200);
                _inv.Add("B01", 5);
                _inv.Add("B02", 5);
                _inv.Add("B03", 5);
                _cur.Set(100, 0);
                Debug.Log("[BaitSelectPanel] 未注入实例，已使用测试数据");
            }
        }

        private static void EnsureConfig()
        {
            if (_config != null) return;
            _config = Resources.Load<FishingConfigSO>(FishingManager.ConfigResourcePath);
            if (_config == null)
            {
                _config = FishingConfigSO.CreateDefault();
                Debug.LogWarning("[BaitSelectPanel] 未找到 FishingConfig 资源，使用默认配置");
            }
        }

        private static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            // ---- Canvas ----
            var canvasGo = new GameObject("BaitSelectCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // 事件系统
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // ---- 半透背景 ----
            var bgGo = CreateUI("BG", canvasGo.transform);
            bgGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            Stretch(bgGo.GetComponent<RectTransform>());

            // ---- 面板根 ----
            _panelRoot = CreateUI("Panel", canvasGo.transform);
            var prt = _panelRoot.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(500, 640);
            prt.anchoredPosition = Vector2.zero;
            _panelRoot.AddComponent<Image>().color = new Color(0.1f, 0.15f, 0.2f, 0.95f);

            // ---- 标题 ----
            MakeText("Title", _panelRoot.transform, "背包", 24, new Vector2(0, 290), TextAnchor.UpperCenter);

            // ---- 提示 ----
            _hintText = MakeText("Hint", _panelRoot.transform, "点击鱼饵选择并进入钓鱼", 16, new Vector2(0, 250), TextAnchor.UpperCenter);

            // ---- 鱼饵区标题 ----
            MakeText("BaitHeader", _panelRoot.transform, "— 鱼饵 —", 16, new Vector2(0, 220), TextAnchor.UpperCenter);

            // ---- 鱼饵列表容器 ----
            var baitListGo = CreateUI("BaitList", _panelRoot.transform);
            var blrt = baitListGo.GetComponent<RectTransform>();
            blrt.anchorMin = new Vector2(0, 0.48f);
            blrt.anchorMax = new Vector2(1, 0.82f);
            blrt.offsetMin = new Vector2(20, 0);
            blrt.offsetMax = new Vector2(-20, 0);
            var bvlg = baitListGo.AddComponent<VerticalLayoutGroup>();
            bvlg.spacing = 6;
            bvlg.padding = new RectOffset(0, 0, 0, 0);
            bvlg.childControlWidth = true;
            bvlg.childControlHeight = true;
            bvlg.childForceExpandWidth = true;
            bvlg.childForceExpandHeight = false;

            // 构建每个鱼饵条目
            foreach (var bait in _config.baits)
            {
                BuildBaitEntry(baitListGo.transform, bait);
            }

            // ---- 钓获区标题 ----
            MakeText("FishHeader", _panelRoot.transform, "— 钓获 —", 16, new Vector2(0, -20), TextAnchor.UpperCenter);

            // ---- 鱼类列表容器 ----
            var fishListGo = CreateUI("FishList", _panelRoot.transform);
            var flrt = fishListGo.GetComponent<RectTransform>();
            flrt.anchorMin = new Vector2(0, 0.12f);
            flrt.anchorMax = new Vector2(1, 0.42f);
            flrt.offsetMin = new Vector2(20, 0);
            flrt.offsetMax = new Vector2(-20, 0);
            var fvlg = fishListGo.AddComponent<VerticalLayoutGroup>();
            fvlg.spacing = 6;
            fvlg.padding = new RectOffset(0, 0, 0, 0);
            fvlg.childControlWidth = true;
            fvlg.childControlHeight = true;
            fvlg.childForceExpandWidth = true;
            fvlg.childForceExpandHeight = false;

            // 构建每个鱼类条目
            if (_config.fishes != null)
            {
                foreach (var fish in _config.fishes)
                {
                    BuildFishEntry(fishListGo.transform, fish);
                }
            }

            // ---- 关闭按钮 ----
            var closeBtn = MakeButton("CloseBtn", _panelRoot.transform, "关闭", 18, new Vector2(0, -290), 180, 44);
            closeBtn.onClick.AddListener(Hide);

            _panelRoot.SetActive(false);
        }

        private static void BuildBaitEntry(Transform parent, BaitData bait)
        {
            var entry = CreateUI("Bait_" + bait.id, parent);
            entry.AddComponent<Image>().color = new Color(0.2f, 0.25f, 0.35f, 0.9f);
            var hlg = entry.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 6, 6);
            hlg.spacing = 10;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // 鱼饵图标（白色占位）
            var icon = CreateUI("Icon", entry.transform);
            var iconRt = icon.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(48, 48);
            icon.AddComponent<Image>().color = Color.white;

            // 名称
            MakeText("Name", entry.transform, bait.name, 18, Vector2.zero, TextAnchor.MiddleLeft);

            // 咬钩率
            MakeText("Rate", entry.transform, "咬钩 " + (bait.biteRate * 100f).ToString("0") + "%", 14, Vector2.zero, TextAnchor.MiddleLeft);

            // 持有数量
            var countT = MakeText("Count", entry.transform, "x0", 16, Vector2.zero, TextAnchor.MiddleRight);
            countT.alignment = TextAnchor.MiddleRight;

            // 选择按钮
            var btn = entry.AddComponent<Button>();
            btn.targetGraphic = entry.GetComponent<Image>();
            string id = bait.id;
            btn.onClick.AddListener(() => OnBaitSelected(id));

            // 缓存数量文本（按 baitId 存储以便刷新）
            _countTexts[bait.id] = countT;
        }

        private static void BuildFishEntry(Transform parent, FishData fish)
        {
            var entry = CreateUI("Fish_" + fish.id, parent);
            entry.AddComponent<Image>().color = new Color(0.15f, 0.2f, 0.3f, 0.9f);
            var hlg = entry.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.spacing = 10;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // 鱼图标（稀有度区分颜色）
            var icon = CreateUI("Icon", entry.transform);
            var iconRt = icon.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(40, 40);
            Color iconColor = fish.rarity == FishRarity.Rare ? new Color(1f, 0.85f, 0.2f) : new Color(0.6f, 0.8f, 1f);
            icon.AddComponent<Image>().color = iconColor;

            // 名称
            MakeText("Name", entry.transform, fish.name, 16, Vector2.zero, TextAnchor.MiddleLeft);

            // 稀有度
            MakeText("Rarity", entry.transform, fish.rarity == FishRarity.Rare ? "稀有" : "普通", 13, Vector2.zero, TextAnchor.MiddleLeft);

            // 售价
            MakeText("Price", entry.transform, fish.sellPrice + "贝", 13, Vector2.zero, TextAnchor.MiddleLeft);

            // 持有数量
            var countT = MakeText("Count", entry.transform, "x0", 15, Vector2.zero, TextAnchor.MiddleRight);
            countT.alignment = TextAnchor.MiddleRight;

            _fishCountTexts[fish.id] = countT;
        }

        /// <summary>选中鱼饵：写入 FishingLoadout 并跳转钓鱼场景。</summary>
        private static void OnBaitSelected(string baitId)
        {
            EnsureInstances();
            int owned = _inv.GetCount(baitId);
            if (owned <= 0)
            {
                if (_hintText != null) _hintText.text = "该鱼饵数量不足，请先在商店购买";
                Debug.Log($"[BaitSelectPanel] 鱼饵 {baitId} 数量不足，无法选择");
                return;
            }

            // 解锁校验
            int unlockLv = _config.unlockLevel;
            int curLv = _pLv != null ? _pLv.Level : 1;
            if (curLv < unlockLv)
            {
                if (_hintText != null) _hintText.text = $"等级 {unlockLv} 解锁钓鱼，当前等级不足";
                Debug.Log($"[BaitSelectPanel] 等级 {unlockLv} 解锁钓鱼，当前 {curLv}，无法进入");
                return;
            }

            // 写入 FishingLoadout（新系统钓鱼会话会读取）
            FishingLoadout.SetLoadout(baitId, _inv, _cur, _pLv);
            Debug.Log($"[BaitSelectPanel] 已选择鱼饵 {baitId}（持有 {owned}）");

            // Step11 界面合一：不再切屏到独立钓鱼界面，钓鱼统一在 FishingScene（池塘鱼群游动动画）内进行。
            Hide();
            ShowFishingHint();
        }

        /// <summary>Step11：提示钓鱼已在钓点场景内进行，不再跳转独立界面。</summary>
        private static void ShowFishingHint()
        {
            if (_hintText != null)
            {
                _hintText.text = "已记录鱼饵，请前往钓鱼场景开始垂钓";
            }
            Debug.Log("[BaitSelectPanel] Step11 界面合一：不再切屏，钓鱼统一在 FishingScene 内进行");
        }

        private static void RefreshCounts()
        {
            if (_inv == null) return;
            foreach (var kv in _countTexts)
            {
                if (kv.Value != null) kv.Value.text = "x" + _inv.GetCount(kv.Key);
            }
            foreach (var kv in _fishCountTexts)
            {
                if (kv.Value != null) kv.Value.text = "x" + _inv.GetCount(kv.Key);
            }
        }

        // ================= UI 辅助 =================

        private static readonly Dictionary<string, Text> _countTexts = new Dictionary<string, Text>();
        private static readonly Dictionary<string, Text> _fishCountTexts = new Dictionary<string, Text>();

        private static GameObject CreateUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text MakeText(string name, Transform parent, string content, int fontSize, Vector2 pos, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(260, fontSize + 6);
            var t = go.GetComponent<Text>();
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }

        private static Button MakeButton(string name, Transform parent, string label, int fontSize, Vector2 pos, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.25f, 0.35f, 0.5f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var lt = lblGo.GetComponent<Text>();
            lt.text = label;
            lt.fontSize = fontSize;
            lt.alignment = TextAnchor.MiddleCenter;
            lt.color = Color.white;
            lt.raycastTarget = false;
            lt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return btn;
        }
    }

    /// <summary>
    /// 鱼饵选择入口组件：挂到任意 Button 上，点击即打开 <see cref="BaitSelectPanel"/>。
    /// 用法：在庭院/主界面任意"钓鱼"按钮上挂此组件，无需手动绑定 onClick。
    /// </summary>
    public class BaitSelectEntry : MonoBehaviour
    {
        private void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(BaitSelectPanel.Show);
            else Debug.LogWarning("[BaitSelectEntry] 未找到 Button 组件，请挂到带 Button 的物体上");
        }
    }
}
