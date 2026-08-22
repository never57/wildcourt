using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WildCourt.Core;
using WildCourt.Gameplay.Fishing;

namespace WildCourt.Gameplay.Fishing
{
    /// <summary>
    /// 钓鱼场景引导：FishingScene 加载后自动搭建全部运行时（无需预制体/手动场景配置）。
    /// 负责：EventSystem、Camera、FishingManager、钓鱼 UI Canvas（提示/等待/收线/结算）。
    ///
    /// 业务流程（严格遵守）：背包选鱼饵 → 写入 FishingLoadout → 跳转 FishingScene
    /// → 本引导读取 FishingLoadout → 自动开面板并垂钓。
    /// 本场景不做鱼饵选择面板，鱼饵配置全部交由背包界面完成。
    /// 本项目没有鱼竿相关逻辑。
    /// </summary>
    public static class FishingSceneBootstrap
    {
        private const string TargetScene = "FishingScene";
        private static bool _booted;

        // Step11 界面合一：禁用运行时自动引导（老系统钓鱼 UI 不再叠加到 FishingScene）。
        // 钓鱼统一由新系统（_Game/Scripts，Game.* 命名空间）在 FishingScene 内提供，
        // 池塘鱼群游动动画全程可见，抛竿/上钩/收线不打断。
        private static void AutoBoot()
        {
            Debug.Log("[FishingSceneBootstrap] 已禁用：老系统钓鱼 UI 不再自动叠加（Step11 界面合一）");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBoot(scene);
        }

        private static void TryBoot(Scene scene)
        {
            if (_booted || scene.name != TargetScene) return;
            _booted = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            EnsureEventSystem();
            EnsureCamera();
            EnsureConfigLoaded();

            // 优先读取背包注入的 FishingLoadout（真实业务流程：背包选鱼饵 → 跳转场景）
            InventoryManager inventory;
            CurrencyManager currency;
            PlayerLevel playerLevel;
            if (FishingLoadout.HasLoadout)
            {
                inventory = FishingLoadout.Inventory;
                currency = FishingLoadout.Currency;
                playerLevel = FishingLoadout.Player;
            }
            else
            {
                // 独立场景测试：自建实例 + 测试数据，并写入 FishingLoadout 以便面板自动开钓
                inventory = new InventoryManager();
                currency = new CurrencyManager();
                playerLevel = new PlayerLevel();
                playerLevel.Init();
                playerLevel.SetExp(200); // 等级2，解锁钓鱼
                inventory.Add("B01", 5);
                inventory.Add("B02", 5);
                inventory.Add("B03", 5);
                currency.Set(100, 0);
                FishingLoadout.SetLoadout("B01", inventory, currency, playerLevel);
            }

            // ---- FishingManager 单例 ----
            var host = new GameObject("FishingManager");
            var mgr = host.AddComponent<FishingManager>();
            mgr.ConfigureContext(inventory, currency, playerLevel, baitId =>
            {
                Debug.Log($"[FishingScene] 商店跳转：购买 {baitId}（本场景无商店，模拟购买 +3 个）");
                inventory.Add(baitId, 3);
                currency.SpendShell(mgr.GetBait(baitId)?.price ?? 0);
                mgr.NotifyBaitCountChanged();
            });

            // ---- 庭院水井（3D 可点击物体，备用入口）----
            CreateWell(mgr);

            // ---- 钓鱼 UI Canvas（纯代码搭建，不依赖预制体）----
            FishingSceneUI.Build(mgr, inventory, currency, playerLevel);

            // 自动打开钓鱼面板：背包已选鱼饵 → 直接进入垂钓
            FishingSceneUI.OpenPanel();

            Debug.Log("[FishingScene] 场景引导完成：已读取背包鱼饵，钓鱼面板已就绪");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam != null) return;
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            var c = camGo.AddComponent<Camera>();
            c.orthographic = true;
            c.orthographicSize = 5f;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.45f, 0.65f, 0.85f, 1f); // 天空蓝
            camGo.transform.position = new Vector3(0, 0, -10);
        }

        private static void EnsureConfigLoaded()
        {
            if (!ConfigMgr.Instance.IsLoaded)
            {
                ConfigMgr.Instance.LoadAll();
            }
        }

        // ================= 水井 =================

        /// <summary>
        /// 创建水井物体：SpriteRenderer + 可点击 Collider2D。
        /// 点击水井 → 解锁校验 → 打开钓鱼面板（备用入口，主流程为自动开面板）。
        /// </summary>
        private static void CreateWell(FishingManager mgr)
        {
            var wellGo = new GameObject("Well");
            wellGo.transform.position = new Vector3(0, -1.5f, 0);

            var sr = wellGo.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSquareSprite();
            sr.color = new Color(0.4f, 0.4f, 0.5f, 1f); // 石灰色
            sr.sortingOrder = 1;

            var col = wellGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.5f, 1.5f);

            var well = wellGo.AddComponent<WellClickable>();
            well.Init(mgr);

            Debug.Log("[FishingScene] 水井已创建（点击水井也可进入钓鱼）");
        }

        /// <summary>生成一个 1x1 白色方形 Sprite（供无贴图物体使用）。</summary>
        private static Sprite _squareSprite;
        private static Sprite MakeSquareSprite()
        {
            if (_squareSprite != null) return _squareSprite;
            var tex = new Texture2D(64, 64);
            var pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            _squareSprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
            return _squareSprite;
        }
    }

    // ================= 水井点击组件 =================

    /// <summary>
    /// 水井可点击组件：OnMouseDown → 调 FishingManager.TryEnterWell。
    /// 未解锁时弹提示；已解锁时打开钓鱼面板。
    /// </summary>
    public class WellClickable : MonoBehaviour
    {
        private FishingManager _mgr;

        public void Init(FishingManager mgr) => _mgr = mgr;

        private void OnMouseDown()
        {
            if (_mgr == null) return;
            bool unlocked = _mgr.TryEnterWell(out int unlockLevel);
            if (!unlocked)
            {
                Debug.Log($"[FishingScene] 等级 {unlockLevel} 解锁钓鱼，当前等级不足");
                FishingSceneUI.ShowMessage($"等级 {unlockLevel} 解锁钓鱼");
                return;
            }
            FishingSceneUI.OpenPanel();
        }
    }

    // ================= 钓鱼场景 UI（纯代码搭建） =================

    /// <summary>
    /// 钓鱼场景 UI：不依赖预制体，全部用代码创建 Canvas/Text/Button/Image 并绑定事件。
    /// 布局：
    /// - 顶栏：等级 / 庭贝 / 关闭
    /// - 中央（左侧）：提示 / 水花 / 出鱼特效 / 结算
    /// - 右侧：竖版判定条（拉力填充 + 鱼图标上下游动 + 捕/逃标记）+ 圆形收杆按钮
    /// 鱼饵数据全部来自 FishingLoadout（背包已选），本面板不做鱼饵选择。
    /// 不存在鱼竿相关逻辑与 UI。
    /// </summary>
    public static class FishingSceneUI
    {
        private static FishingManager _mgr;
        private static InventoryManager _inv;
        private static CurrencyManager _cur;
        private static PlayerLevel _pLv;

        // ---- UI 引用 ----
        private static GameObject _panelRoot;
        private static Text _hintText;
        private static Text _shellText;
        private static Text _levelText;
        private static GameObject _splashVfx;
        private static GameObject _catchVfx;
        private static GameObject _resultRoot;
        private static Text _resultNameText;
        private static Text _resultRarityText;
        private static Text _resultPriceText;
        private static Text _resultStateText;
        private static Button _againBtn;
        private static Button _closeBtn;
        private static Button _reelBtn;
        private static GameObject _lockedRoot;
        private static Text _lockedText;
        private static Button _lockedCloseBtn;
        private static GameObject _msgRoot;
        private static Text _msgText;

        // ---- 右侧竖版判定条 ----
        private static RectTransform _barBg;          // 竖条背景（国风底色）
        private static RectTransform _tensionFill;    // 拉力填充（从底部向上）
        private static RectTransform _fishIconRt;     // 鱼图标（在竖条内上下游动）
        private static GameObject _captureMark;       // 捕标记（顶部，成功阈值）
        private static GameObject _escapeMark;         // 逃标记（底部，失败阈值）
        private static Image _reelBtnImage;           // 圆形收杆按钮图（切换可用态着色）
        private const float BarHeight = 420f;         // 竖条可视高度
        private const float BarWidth = 70f;            // 竖条宽度
        private const float BarMargin = 28f;           // 竖条上下内边距（避开捕/逃标记）
        private const float ReelBtnSize = 120f;        // 圆形收杆按钮直径

        // ---- 收线运行时状态 ----
        private static float _currentProgress;         // 当前拉力（0~1）
        private static float _struggleJitter;          // 鱼挣扎位移抖动（临时）

        private static bool _subscribed;
        private static int _lastWaitHintSec = -1;

        // ================= 构建 =================

        public static void Build(FishingManager mgr, InventoryManager inv, CurrencyManager cur, PlayerLevel pLv)
        {
            _mgr = mgr;
            _inv = inv;
            _cur = cur;
            _pLv = pLv;

            // ---- Canvas ----
            var canvasGo = new GameObject("FishingCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f; // 多分辨率适配：宽高各占一半权重
            canvasGo.AddComponent<GraphicRaycaster>();

            // ---- 背景 ----
            var bgGo = CreateUI("Background", canvasGo.transform);
            bgGo.AddComponent<Image>().color = new Color(0.15f, 0.2f, 0.3f, 0.9f);
            Stretch(bgGo.GetComponent<RectTransform>());

            // ---- 顶栏：等级 / 庭贝 / 关闭 ----
            var topBar = CreateUI("TopBar", canvasGo.transform);
            var tbrt = topBar.GetComponent<RectTransform>();
            tbrt.anchorMin = new Vector2(0, 1);
            tbrt.anchorMax = new Vector2(1, 1);
            tbrt.pivot = new Vector2(0.5f, 1);
            tbrt.sizeDelta = new Vector2(0, 50);
            tbrt.anchoredPosition = Vector2.zero;

            _levelText = MakeText("LevelText", topBar.transform, "Lv ?", 18, new Vector2(-400, 0), TextAnchor.MiddleLeft);
            _shellText = MakeText("ShellText", topBar.transform, "庭贝 0", 18, new Vector2(-200, 0), TextAnchor.MiddleLeft);
            _closeBtn = MakeButton("CloseBtn", topBar.transform, "关闭", 16, new Vector2(550, 0), 80, 40);
            _closeBtn.onClick.AddListener(() => { _panelRoot.SetActive(false); _mgr.MarkInterrupted(); });

            // ---- 钓鱼面板根（占满顶栏以下全部区域）----
            _panelRoot = CreateUI("FishingPanel", canvasGo.transform);
            var prt = _panelRoot.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = new Vector2(0, -50);
            _panelRoot.SetActive(false);

            // ---- 中央区域：提示 / 特效 / 结算（左侧，占 62% 宽）----
            var center = CreateUI("CenterPanel", _panelRoot.transform);
            var crt = center.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0);
            crt.anchorMax = new Vector2(0.62f, 1f);
            crt.offsetMin = new Vector2(10, 10);
            crt.offsetMax = new Vector2(-5, -10);
            center.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.85f);

            // 提示文本
            _hintText = MakeText("HintText", center.transform, "等待开始垂钓…", 18, new Vector2(0, 200), TextAnchor.UpperCenter);

            // 水花特效位（咬钩前1秒）
            _splashVfx = CreateUI("SplashVfx", center.transform);
            var srt = _splashVfx.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(200, 50);
            srt.anchoredPosition = new Vector2(0, 50);
            _splashVfx.AddComponent<Image>().color = new Color(0.6f, 0.8f, 1f, 0.7f);
            _splashVfx.SetActive(false);

            // 出鱼动画位
            _catchVfx = CreateUI("CatchVfx", center.transform);
            var cvrt = _catchVfx.GetComponent<RectTransform>();
            cvrt.sizeDelta = new Vector2(200, 100);
            cvrt.anchoredPosition = new Vector2(0, 50);
            _catchVfx.AddComponent<Image>().color = new Color(1f, 0.8f, 0.3f, 0.8f);
            _catchVfx.SetActive(false);

            // ---- 结算面板 ----
            _resultRoot = CreateUI("ResultPanel", center.transform);
            var rsrt = _resultRoot.GetComponent<RectTransform>();
            rsrt.sizeDelta = new Vector2(350, 200);
            rsrt.anchoredPosition = new Vector2(0, 0);
            _resultRoot.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.95f);
            _resultRoot.SetActive(false);
            var resultAutoClose = _resultRoot.AddComponent<ResultAutoClose>();
            resultAutoClose.delay = 5f;
            resultAutoClose.OnAutoClose = () =>
            {
                if (_resultRoot != null) _resultRoot.SetActive(false);
                if (_mgr != null && _mgr.Fsm.Current == FishingState.Settlement)
                {
                    _mgr.ConfirmSettlement();
                }
            };

            MakeText("ResultTitle", _resultRoot.transform, "结算", 18, new Vector2(0, 75), TextAnchor.UpperCenter);
            _resultNameText = MakeText("ResultName", _resultRoot.transform, "", 20, new Vector2(0, 35), TextAnchor.MiddleCenter);
            _resultRarityText = MakeText("ResultRarity", _resultRoot.transform, "", 16, new Vector2(0, 5), TextAnchor.MiddleCenter);
            _resultPriceText = MakeText("ResultPrice", _resultRoot.transform, "", 16, new Vector2(0, -20), TextAnchor.MiddleCenter);
            _resultStateText = MakeText("ResultState", _resultRoot.transform, "", 14, new Vector2(0, -45), TextAnchor.MiddleCenter);
            _againBtn = MakeButton("AgainBtn", _resultRoot.transform, "再来一次", 16, new Vector2(0, -75), 200, 40);
            _againBtn.onClick.AddListener(OnAgain);

            // ---- 右侧：竖版判定条 + 圆形收杆按钮 ----
            BuildRightBar(_panelRoot.transform);

            // ---- 锁定提示（等级不足时）----
            _lockedRoot = CreateUI("LockedPanel", canvasGo.transform);
            var lkrt = _lockedRoot.GetComponent<RectTransform>();
            lkrt.sizeDelta = new Vector2(400, 150);
            _lockedRoot.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.95f);
            _lockedRoot.SetActive(false);
            _lockedText = MakeText("LockedText", _lockedRoot.transform, "", 18, new Vector2(0, 20), TextAnchor.MiddleCenter);
            _lockedCloseBtn = MakeButton("LockedClose", _lockedRoot.transform, "知道了", 16, new Vector2(0, -40), 150, 35);
            _lockedCloseBtn.onClick.AddListener(() => _lockedRoot.SetActive(false));

            // ---- 消息提示（临时浮动消息，5 秒后自动消失）----
            _msgRoot = CreateUI("MsgPanel", canvasGo.transform);
            var msgRt = _msgRoot.GetComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0.5f, 0.5f);
            msgRt.anchorMax = new Vector2(0.5f, 0.5f);
            msgRt.sizeDelta = new Vector2(400, 60);
            msgRt.anchoredPosition = new Vector2(0, 100);
            _msgRoot.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            _msgRoot.AddComponent<AutoHideAfterDelay>(); // 5 秒自动隐藏
            _msgRoot.SetActive(false);
            _msgText = MakeText("MsgText", _msgRoot.transform, "", 16, Vector2.zero, TextAnchor.MiddleCenter);

            // ---- 订阅事件 ----
            SubscribeEvents();

            // ---- 初始刷新 ----
            RefreshShell();
            RefreshLevel();
            UpdateReelButtonInteractable();
        }

        // ================= 右侧竖版判定条 =================

        private static void BuildRightBar(Transform parent)
        {
            // 右侧容器（占 38% 宽，满高）
            var right = CreateUI("RightPanel", parent);
            var rprt = right.GetComponent<RectTransform>();
            rprt.anchorMin = new Vector2(0.62f, 0);
            rprt.anchorMax = new Vector2(1f, 1f);
            rprt.offsetMin = new Vector2(5, 10);
            rprt.offsetMax = new Vector2(-10, -10);
            right.AddComponent<Image>().color = new Color(0.1f, 0.15f, 0.2f, 0.8f);

            // 竖条标题
            MakeText("BarTitle", right.transform, "判定条", 18, new Vector2(0, 150), TextAnchor.UpperCenter);

            // ---- 竖条背景（国风底色：深木暖色）----
            var barGo = CreateUI("BarBg", right.transform);
            _barBg = barGo.GetComponent<RectTransform>();
            _barBg.anchorMin = new Vector2(0.5f, 0.5f);
            _barBg.anchorMax = new Vector2(0.5f, 0.5f);
            _barBg.pivot = new Vector2(0.5f, 0.5f);
            _barBg.sizeDelta = new Vector2(BarWidth, BarHeight);
            _barBg.anchoredPosition = new Vector2(0, 40); // 上移，给下方收杆按钮留空间
            barGo.AddComponent<Image>().color = new Color(0.25f, 0.16f, 0.1f, 0.95f);

            // ---- 拉力填充（从底部向上，随拉力增长）----
            var fillGo = CreateUI("TensionFill", barGo.transform);
            _tensionFill = fillGo.GetComponent<RectTransform>();
            _tensionFill.anchorMin = new Vector2(0, 0);
            _tensionFill.anchorMax = new Vector2(1, 0);
            _tensionFill.pivot = new Vector2(0.5f, 0);
            _tensionFill.offsetMin = new Vector2(4, 4);
            _tensionFill.offsetMax = new Vector2(-4, 4); // 初始高度近 0
            fillGo.AddComponent<Image>().color = new Color(0.95f, 0.7f, 0.25f, 0.9f); // 暖金色

            // ---- 捕标记（顶部，成功阈值区）----
            var capGo = CreateUI("CaptureMark", barGo.transform);
            var capRt = capGo.GetComponent<RectTransform>();
            capRt.anchorMin = new Vector2(0, 1);
            capRt.anchorMax = new Vector2(1, 1);
            capRt.pivot = new Vector2(0.5f, 1);
            capRt.offsetMin = new Vector2(4, -26);
            capRt.offsetMax = new Vector2(-4, -2);
            capGo.AddComponent<Image>().color = new Color(0.2f, 0.9f, 0.3f, 0.85f); // 绿色
            MakeText("CaptureText", capGo.transform, "捕", 16, Vector2.zero, TextAnchor.MiddleCenter);
            _captureMark = capGo;

            // ---- 逃标记（底部，失败阈值区）----
            var escGo = CreateUI("EscapeMark", barGo.transform);
            var escRt = escGo.GetComponent<RectTransform>();
            escRt.anchorMin = new Vector2(0, 0);
            escRt.anchorMax = new Vector2(1, 0);
            escRt.pivot = new Vector2(0.5f, 0);
            escRt.offsetMin = new Vector2(4, 2);
            escRt.offsetMax = new Vector2(-4, 26);
            escGo.AddComponent<Image>().color = new Color(0.9f, 0.2f, 0.2f, 0.85f); // 红色
            MakeText("EscapeText", escGo.transform, "逃", 16, Vector2.zero, TextAnchor.MiddleCenter);
            _escapeMark = escGo;

            // ---- 鱼图标（在竖条内上下游动，白色占位）----
            var fishGo = CreateUI("FishIcon", barGo.transform);
            _fishIconRt = fishGo.GetComponent<RectTransform>();
            _fishIconRt.anchorMin = new Vector2(0, 0.5f);
            _fishIconRt.anchorMax = new Vector2(1, 0.5f);
            _fishIconRt.pivot = new Vector2(0.5f, 0.5f);
            _fishIconRt.sizeDelta = new Vector2(0, 40);
            _fishIconRt.anchoredPosition = new Vector2(0, 0);
            fishGo.AddComponent<Image>().color = Color.white; // 白色占位图

            // ---- 圆形收杆按钮（竖条下方，连点增加拉力）----
            var reelGo = CreateUI("ReelBtn", right.transform);
            var reelRt = reelGo.GetComponent<RectTransform>();
            reelRt.anchorMin = new Vector2(0.5f, 0);
            reelRt.anchorMax = new Vector2(0.5f, 0);
            reelRt.pivot = new Vector2(0.5f, 0);
            reelRt.sizeDelta = new Vector2(ReelBtnSize, ReelBtnSize);
            reelRt.anchoredPosition = new Vector2(0, 20);
            _reelBtnImage = reelGo.AddComponent<Image>();
            _reelBtnImage.sprite = MakeCircleSprite(); // 圆形遮罩
            _reelBtnImage.color = new Color(0.85f, 0.6f, 0.2f, 1f); // 国风暖金
            _reelBtn = reelGo.AddComponent<Button>();
            _reelBtn.targetGraphic = _reelBtnImage;
            MakeText("ReelLabel", reelGo.transform, "收杆", 22, Vector2.zero, TextAnchor.MiddleCenter);
            _reelBtn.onClick.AddListener(OnReelClick);
        }

        // ================= 事件订阅 =================

        private static void SubscribeEvents()
        {
            if (_subscribed) return;
            _subscribed = true;
            _mgr.EStateChanged += OnStateChanged;
            _mgr.EWaitTick += OnWaitTick;
            _mgr.ESplashWarning += OnSplashWarning;
            _mgr.EBiteHit += OnBiteHit;
            _mgr.EReelProgress += OnReelProgress;
            _mgr.EReelFailure += OnReelFailure;
            _mgr.EStruggle += OnStruggle;
            _mgr.EResult += OnResult;
            _mgr.EBaitCountChanged += OnBaitCountChanged;
            _mgr.EFishInventoryChanged += OnFishInventoryChanged;
        }

        // ================= 交互回调 =================

        /// <summary>圆形收杆按钮点击：咬钩态进入收线，收线态连点充能。</summary>
        private static void OnReelClick()
        {
            if (_mgr == null) return;
            if (_mgr.Fsm.Current == FishingState.Hooked)
            {
                _mgr.StartReeling();
                _mgr.RegisterReelTap();
            }
            else if (_mgr.Fsm.Current == FishingState.Reeling)
            {
                _mgr.RegisterReelTap();
            }
        }

        /// <summary>「再来一次」：结算后回待机，读取 FishingLoadout 重新抛竿。</summary>
        private static void OnAgain()
        {
            if (_mgr == null) return;
            _mgr.ConfirmSettlement();
            TryStartFishingFromLoadout();
        }

        // ================= 事件回调 =================

        private static void OnStateChanged(FishingState from, FishingState to)
        {
            // 状态切换日志已在 FishingManager 内打印，此处负责 UI 同步
            if (to == FishingState.Idle)
            {
                HideVfx();
                if (_resultRoot != null) _resultRoot.SetActive(false);
            }
            if (from == FishingState.Casting && to == FishingState.Waiting)
            {
                SetHint("浮漂入水，留意咬钩…");
            }
            if (to == FishingState.Hooked)
            {
                SetHint("有鱼咬钩！快速连点「收杆」按钮！");
                _currentProgress = 0f;
                UpdateTensionFill(0f);
                UpdateFishIconPosition();
            }
            if (to == FishingState.Settlement)
            {
                if (_resultRoot != null) _resultRoot.SetActive(true);
            }
            UpdateReelButtonInteractable();
        }

        private static void OnWaitTick(float remain)
        {
            int sec = Mathf.CeilToInt(remain);
            if (sec != _lastWaitHintSec)
            {
                _lastWaitHintSec = sec;
                SetHint(string.Format("等待咬钩… {0} 秒", Mathf.Max(0, sec)));
            }
        }

        private static void OnSplashWarning()
        {
            if (_splashVfx != null) _splashVfx.SetActive(true);
            SetHint("水花预警！即将咬钩！");
        }

        private static void OnBiteHit()
        {
            SetHint("鱼咬钩了！快速连点「收杆」按钮！");
            if (_splashVfx != null) _splashVfx.SetActive(false);
        }

        private static void OnReelProgress(float p)
        {
            _currentProgress = p;
            UpdateTensionFill(p);
            UpdateFishIconPosition();
        }

        private static void OnReelFailure()
        {
            SetHint("脱钩了……");
        }

        /// <summary>鱼挣扎挣脱：鱼图标在竖条内随机改变位置。</summary>
        private static void OnStruggle()
        {
            // 鱼挣扎时叠加随机抖动（拉力下降已由 manager 处理，此处仅表现位移）
            _struggleJitter = Random.Range(-40f, 40f);
            UpdateFishIconPosition();
        }

        private static void OnResult(FishingResult result)
        {
            if (_catchVfx != null) _catchVfx.SetActive(result.Success);
            if (_resultRoot != null)
            {
                _resultRoot.SetActive(true);
                var ac = _resultRoot.GetComponent<ResultAutoClose>();
                if (ac != null) ac.Restart();
            }
            if (result.Success && result.Fish != null)
            {
                if (_resultNameText != null) _resultNameText.text = result.Fish.name;
                if (_resultRarityText != null) _resultRarityText.text = result.Fish.rarity == FishRarity.Rare ? "稀有" : "普通";
                if (_resultPriceText != null) _resultPriceText.text = "售价 " + result.Price + " 庭贝";
                if (_resultStateText != null) _resultStateText.text = "钓获成功，已存入背包";
            }
            else
            {
                if (_resultNameText != null) _resultNameText.text = "—";
                if (_resultRarityText != null) _resultRarityText.text = "—";
                if (_resultPriceText != null) _resultPriceText.text = "";
                if (_resultStateText != null) _resultStateText.text = result.Reason;
            }
            RefreshShell();
            UpdateReelButtonInteractable();
        }

        private static void OnBaitCountChanged()
        {
            // 鱼饵数量变化时刷新庭贝（商店购买可能扣币）
            RefreshShell();
        }

        private static void OnFishInventoryChanged()
        {
            // 鱼类库存变化（钓获存入背包），刷新庭贝
            RefreshShell();
        }

        // ================= 刷新 / 表现 =================

        private static void RefreshShell()
        {
            if (_shellText != null && _cur != null)
                _shellText.text = "庭贝 " + _cur.Shell;
        }

        private static void RefreshLevel()
        {
            if (_levelText != null && _pLv != null)
                _levelText.text = "Lv " + _pLv.Level;
        }

        /// <summary>更新拉力填充高度（从底部向上，随拉力增长）。</summary>
        private static void UpdateTensionFill(float progress)
        {
            if (_tensionFill == null) return;
            float usableHeight = BarHeight - BarMargin * 2f;
            float h = Mathf.Clamp01(progress) * usableHeight;
            _tensionFill.sizeDelta = new Vector2(_tensionFill.sizeDelta.x, h);
        }

        /// <summary>更新鱼图标在竖条内的纵向位置（随拉力上下游动 + 抖动）。</summary>
        private static void UpdateFishIconPosition()
        {
            if (_fishIconRt == null) return;
            // 鱼图标位置 = 底部边距 + 拉力 * 可用高度 - 半高（居中锚点）
            float usableHeight = BarHeight - BarMargin * 2f;
            float baseY = -BarHeight / 2f + BarMargin + Mathf.Clamp01(_currentProgress) * usableHeight;
            float y = Mathf.Clamp(baseY + _struggleJitter, -BarHeight / 2f + BarMargin, BarHeight / 2f - BarMargin);
            _fishIconRt.anchoredPosition = new Vector2(0, y);
        }

        /// <summary>根据当前状态切换圆形收杆按钮可用态着色。</summary>
        private static void UpdateReelButtonInteractable()
        {
            if (_reelBtnImage == null || _mgr == null) return;
            var st = _mgr.Fsm.Current;
            bool active = st == FishingState.Hooked || st == FishingState.Reeling;
            _reelBtnImage.color = active
                ? new Color(0.95f, 0.7f, 0.25f, 1f)   // 暖金高亮
                : new Color(0.4f, 0.4f, 0.4f, 1f);    // 灰色不可用
        }

        // ================= 公共方法 =================

        /// <summary>打开钓鱼面板：读取 FishingLoadout 已选鱼饵，自动开始垂钓。</summary>
        public static void OpenPanel()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
                _mgr?.CancelInterruption();
                RefreshShell();
                RefreshLevel();
                UpdateReelButtonInteractable();
                // 读取背包已选鱼饵，自动开始垂钓
                TryStartFishingFromLoadout();
            }
        }

        /// <summary>从 FishingLoadout 读取已选鱼饵并抛竿；未选/不足则提示且无法开始。</summary>
        private static void TryStartFishingFromLoadout()
        {
            if (_mgr == null) return;
            if (_mgr.Fsm.Current != FishingState.Idle)
            {
                // 正在钓鱼中，不重复开始
                return;
            }
            string baitId = FishingLoadout.SelectedBaitId;
            if (string.IsNullOrEmpty(baitId) || !FishingLoadout.HasSelectedBaitInStock())
            {
                Debug.Log("[FishingScene] 未选择鱼饵或鱼饵数量不足，无法开始垂钓");
                SetHint("未选择鱼饵，无法开始垂钓（请先在背包选择鱼饵）");
                return;
            }
            _mgr.SelectBait(baitId);
            bool ok = _mgr.Cast(baitId);
            if (ok) SetHint("抛竿成功，等待咬钩…");
            else SetHint("鱼饵数量不足，无法开始垂钓");
        }

        public static void ShowMessage(string msg)
        {
            if (_msgText != null) _msgText.text = msg;
            if (_msgRoot != null)
            {
                _msgRoot.SetActive(true);
                // 重新显示时重置 5 秒自动隐藏计时
                _msgRoot.GetComponent<AutoHideAfterDelay>()?.Restart();
            }
        }

        // ================= UI 辅助 =================

        private static void SetHint(string msg)
        {
            if (_hintText != null) _hintText.text = msg;
        }

        private static void HideVfx()
        {
            if (_splashVfx != null) _splashVfx.SetActive(false);
            if (_catchVfx != null) _catchVfx.SetActive(false);
        }

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
            rt.sizeDelta = new Vector2(200, fontSize + 4);
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

        /// <summary>生成圆形 Sprite（用于圆形收杆按钮遮罩）。</summary>
        private static Sprite _circleSprite;
        private static Sprite MakeCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            int size = 128;
            var tex = new Texture2D(size, size);
            var pixels = new Color32[size * size];
            float center = (size - 1) / 2f;
            float radius = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    bool inside = (dx * dx + dy * dy) <= (radius * radius);
                    pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _circleSprite;
        }
    }

    /// <summary>
    /// 临时浮动提示自动隐藏：激活后 delay 秒（默认 5 秒）自动关闭所在 GameObject。
    /// 通过 OnEnable 自动启动；外部可调用 Restart 重置计时。
    /// </summary>
    public class AutoHideAfterDelay : MonoBehaviour
    {
        public float delay = 5f;
        private Coroutine _co;

        public void Restart()
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(HideAfter());
        }

        void OnEnable()
        {
            Restart();
        }

        void OnDisable()
        {
            if (_co != null) StopCoroutine(_co);
            _co = null;
        }

        IEnumerator HideAfter()
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 结算面板自动关闭：激活后 delay 秒（默认 5 秒）触发回调关闭结算面板并确认结算。
    /// 通过 OnEnable 自动启动；外部可调用 Restart 重置计时。
    /// </summary>
    public class ResultAutoClose : MonoBehaviour
    {
        public float delay = 5f;
        public System.Action OnAutoClose;
        private Coroutine _co;

        public void Restart()
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(WaitAndClose());
        }

        void OnEnable()
        {
            Restart();
        }

        void OnDisable()
        {
            if (_co != null) StopCoroutine(_co);
            _co = null;
        }

        IEnumerator WaitAndClose()
        {
            yield return new WaitForSeconds(delay);
            OnAutoClose?.Invoke();
        }
    }
}
