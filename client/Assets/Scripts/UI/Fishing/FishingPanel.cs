using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WildCourt.Gameplay.Fishing;

namespace WildCourt.UI.Fishing
{
    /// <summary>
    /// 钓鱼界面（预制体驱动，同 FarmPanel 规范：UI 结构由 FishingPanel.prefab 在编辑器搭建，
    /// 本类只做实例化/绑定字段与事件刷新，不代码建 UI）。
    ///
    /// 预制体搭建要求（Unity 会话按此创建 FishingPanel.prefab）：
    ///   根节点挂 FishingPanel；序列化字段：panelRoot / closeBtn / hintText / shellText /
    ///   baitListRoot / baitItemPrefab / reelProgress (Slider) / splashVfx / catchVfx /
    ///   resultRoot / resultNameText / resultRarityText / resultPriceText / resultStateText /
    ///   shopBtn / againBtn / fishListRoot / fishItemPrefab / totalSellText / sellAllBtn。
    ///   baitItemPrefab 挂 BaitSlot（name/price/count/按钮，见 BaitSlot）。
    /// </summary>
    public class FishingPanel : MonoBehaviour
    {
        public static FishingPanel Instance { get; private set; }

        /// <summary>跨场景注入的 FishingPanel 预制体（庭院水井入口注入，支持热更）。</summary>
        public static GameObject PanelPrefab { get; set; }

        [Header("根/通用")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeBtn;
        [SerializeField] private Text hintText;
        [SerializeField] private Text shellText;

        [Header("选饵")]
        [SerializeField] private Transform baitListRoot;
        [SerializeField] private GameObject baitItemPrefab;

        [Header("等待/咬钩/收线")]
        [SerializeField] private Slider reelProgress;
        [SerializeField] private GameObject splashVfx;   // 咬钩前水花(1s)
        [SerializeField] private GameObject catchVfx;    // 出鱼动画位

        [Header("结算")]
        [SerializeField] private GameObject resultRoot;
        [SerializeField] private Text resultNameText;
        [SerializeField] private Text resultRarityText;
        [SerializeField] private Text resultPriceText;
        [SerializeField] private Text resultStateText;
        [SerializeField] private Button againBtn;

        [Header("店铺/批量出售")]
        [SerializeField] private Button shopBtn;
        [SerializeField] private Transform fishListRoot;
        [SerializeField] private GameObject fishItemPrefab;
        [SerializeField] private Text totalSellText;
        [SerializeField] private Button sellAllBtn;

        private readonly Dictionary<string, BaitSlot> _baitSlots = new Dictionary<string, BaitSlot>();

        private FishingManager Mgr => FishingManager.Instance;
        private bool _subscribed;
        private Coroutine _autoCloseRoutine;

        public static FishingPanel CreateOrInstantiate()
        {
            if (PanelPrefab == null)
            {
                Debug.LogWarning("[FishingPanel] 未注入 FishingPanel 预制体（庭院水井入口注入），跳过打开");
                return null;
            }
            if (Instance != null)
            {
                Instance.Open();
                return Instance;
            }
            var go = Instantiate(PanelPrefab);
            var panel = go.GetComponent<FishingPanel>();
            if (panel == null)
            {
                Destroy(go);
                Debug.LogWarning("[FishingPanel] 预制体缺少 FishingPanel 组件");
                return null;
            }
            return panel;
        }

        public void Open()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            // 重新打开，清除离场中断打点（若离场已超过阈值，管理器已收束结算）
            if (Mgr != null) Mgr.CancelInterruption();
            RefreshShell();
            RefreshAll();
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            SubscribeEvents();
            BindButtons();
            BuildBaitList();
            RefreshAll();
            SetHint("选择鱼饵，点击抛竿开始钓鱼");
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            _subscribed = true;
            if (Mgr == null) return;
            Mgr.EStateChanged += OnStateChanged;
            Mgr.EWaitTick += OnWaitTick;
            Mgr.ESplashWarning += OnSplashWarning;
            Mgr.EBiteHit += OnBiteHit;
            Mgr.EReelProgress += OnReelProgress;
            Mgr.EReelFailure += OnReelFailure;
            Mgr.EResult += OnResult;
            Mgr.EBaitCountChanged += RefreshBaitCounts;
            Mgr.EFishInventoryChanged += RefreshShell;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (Mgr == null) return;
            Mgr.EStateChanged -= OnStateChanged;
            Mgr.EWaitTick -= OnWaitTick;
            Mgr.ESplashWarning -= OnSplashWarning;
            Mgr.EBiteHit -= OnBiteHit;
            Mgr.EReelProgress -= OnReelProgress;
            Mgr.EReelFailure -= OnReelFailure;
            Mgr.EResult -= OnResult;
            Mgr.EBaitCountChanged -= RefreshBaitCounts;
            Mgr.EFishInventoryChanged -= RefreshShell;
        }

        private void BindButtons()
        {
            if (closeBtn != null) closeBtn.onClick.AddListener(OnClosePanel);
            if (againBtn != null) againBtn.onClick.AddListener(OnAgain);
            if (shopBtn != null) shopBtn.onClick.AddListener(OnOpenShop);
            if (sellAllBtn != null) sellAllBtn.onClick.AddListener(OnSellAll);
        }

        // ================= 选饵 =================

        private void BuildBaitList()
        {
            if (Mgr == null || baitListRoot == null || baitItemPrefab == null) return;
            foreach (var bait in Mgr.Baits)
            {
                var go = Instantiate(baitItemPrefab, baitListRoot);
                var slot = go.GetComponent<BaitSlot>();
                if (slot == null)
                {
                    Destroy(go);
                    continue;
                }
                // 展示：名称 / 售价 / 持有数量；不足时置灰 + 跳商店
                slot.Bind(bait, Mgr.GetBaitCount(bait.id), OnBaitClicked, OnShopForBait);
                _baitSlots[bait.id] = slot;
            }
        }

        private void OnBaitClicked(string baitId)
        {
            if (Mgr == null) return;
            Mgr.SelectBait(baitId);
            // 已选饵处于抛竿位：立即抛竿（文档：选饵后点抛竿）
            if (Mgr.GetBaitCount(baitId) > 0 && Mgr.Cast(baitId))
            {
                SetHint("抛竿成功，等待咬钩…");
            }
            else
            {
                SetHint("鱼饵不足，去商店购买");
            }
            RefreshBaitCounts();
        }

        private void OnShopForBait(string baitId)
        {
            if (Mgr != null && Mgr.Context != null && Mgr.Context.OpenShopForBait(baitId))
            {
                SetHint("已跳转商店购买鱼饵");
            }
        }

        private void OnOpenShop()
        {
            if (Mgr == null) return;
            string baitId = Mgr.CurrentBaitId;
            if (!string.IsNullOrEmpty(baitId))
            {
                OnShopForBait(baitId);
            }
            else
            {
                SetHint("请先选择鱼饵");
            }
        }

        private void RefreshBaitCounts()
        {
            if (Mgr == null) return;
            foreach (var kv in _baitSlots)
            {
                kv.Value.SetCount(Mgr.GetBaitCount(kv.Key), Mgr.GetBait(kv.Key));
            }
        }

        // ================= 状态事件 =================

        private void OnStateChanged(FishingState from, FishingState to)
        {
            // 结算回待机后清场并刷新持有量
            if (to == FishingState.Idle)
            {
                HideVfx();
                if (resultRoot != null) resultRoot.SetActive(false);
                RefreshAll();
            }
            // 抛竿入水后进入等待：可给 hint 展示
            if (from == FishingState.Casting && to == FishingState.Waiting)
            {
                SetHint("浮漂入水，留意咬钩…");
            }
        }

        private float _lastWaitHintSec = -1f;

        private void OnWaitTick(float remain)
        {
            // 每秒整秒刷新一次提示，避免等待环每帧 string.Format 造成 GC（性能要求）
            int sec = Mathf.CeilToInt(remain);
            if (sec != _lastWaitHintSec)
            {
                _lastWaitHintSec = sec;
                SetHint(string.Format("等待咬钩… {0} 秒", Mathf.Max(0, sec)));
            }
        }

        private void OnSplashWarning()
        {
            if (splashVfx != null) splashVfx.SetActive(true);
        }

        private void OnBiteHit()
        {
            SetHint("有鱼咬钩！快速连点收线！");
            // 收线进度 UI 归零待命
            if (reelProgress != null) reelProgress.value = 0f;
        }

        private void OnReelProgress(float p)
        {
            if (reelProgress != null) reelProgress.value = p;
        }

        private void OnReelFailure()
        {
            SetHint("脱钩了……");
        }

        private void OnResult(Gameplay.Fishing.FishingResult result)
        {
            if (catchVfx != null) catchVfx.SetActive(result.Success); // 出鱼动画（成功后播放）
            if (resultRoot != null) resultRoot.SetActive(true);
            SetResultText(result);
            // 结算界面弹出 5 秒后自动关闭
            if (_autoCloseRoutine != null) StopCoroutine(_autoCloseRoutine);
            _autoCloseRoutine = StartCoroutine(AutoCloseResult());
        }

        private IEnumerator AutoCloseResult()
        {
            yield return new WaitForSeconds(5f);
            if (resultRoot != null) resultRoot.SetActive(false);
            if (Mgr != null && Mgr.Fsm.Current == FishingState.Settlement)
            {
                Mgr.ConfirmSettlement();
            }
            _autoCloseRoutine = null;
        }

        private void SetResultText(Gameplay.Fishing.FishingResult result)
        {
            if (result.Success && result.Fish != null)
            {
                if (resultNameText != null) resultNameText.text = result.Fish.name;
                if (resultRarityText != null) resultRarityText.text = result.Fish.rarity == FishRarity.Rare ? "稀有" : "普通";
                if (resultPriceText != null) resultPriceText.text = "售价 " + result.Price + " 庭贝";
                if (resultStateText != null) resultStateText.text = "钓获成功，已存入背包";
            }
            else
            {
                if (resultNameText != null) resultNameText.text = "—";
                if (resultRarityText != null) resultRarityText.text = "—";
                if (resultPriceText != null) resultPriceText.text = "";
                if (resultStateText != null) resultStateText.text = result.Reason;
            }
        }

        // ================= 收线 / 结算操作 =================

        /// <summary>绑定到「点击收线」触点（咬钩时按钮/整屏点击）。</summary>
        public void OnReelClick()
        {
            if (Mgr == null || Mgr.Fsm.Current != FishingState.Hooked)
            {
                // 收线中 → 视为连点充能
                Mgr?.RegisterReelTap();
                return;
            }
            Mgr.StartReeling();
            Mgr.RegisterReelTap();
        }

        private void OnAgain()
        {
            if (Mgr == null) return;
            Mgr.ConfirmSettlement();
            bool ok = Mgr.CastAgain();
            if (ok) SetHint("再次抛竿，等待咬钩…");
            else SetHint("鱼饵不足，去商店购买");
            RefreshAll();
        }

        private void OnClosePanel()
        {
            // 切出界面 >10s 视为中断（此处立即打点，由管理器判定是否超阈值）
            if (Mgr != null && Mgr.Fsm.IsActive) Mgr.MarkInterrupted();
            Close();
        }

        // ================= 批量出售 =================

        private void OnSellAll()
        {
            if (Mgr == null || Mgr.Fishes == null || fishListRoot == null) return;
            int total = 0;
            // 遍历全部鱼种配置，把持有 >0 的鱼种整组出售
            foreach (var fish in Mgr.Fishes)
            {
                total += Mgr.SellAllOf(fish.id);
            }
            SetHint("已出售鱼群，收入 " + total + " 庭贝");
            RefreshShell();
        }

        private void RefreshAll()
        {
            RefreshBaitCounts();
            RefreshShell();
        }

        private void RefreshShell()
        {
            // 庭贝实时显示：优先走 FishingMoney 命中；无则从 Inventory 侧同步。
            // 此处简化：通过 EventBus 或直接由外部 CurrencyManager 刷新，占位留待接入。
            if (shellText != null && Mgr != null && Mgr.Context is DefaultFishingContext)
            {
                var cur = ((DefaultFishingContext)Mgr.Context).Currency;
                if (cur != null) shellText.text = "庭贝 " + cur.Shell;
            }
        }

        private void HideVfx()
        {
            if (splashVfx != null) splashVfx.SetActive(false);
            if (catchVfx != null) catchVfx.SetActive(false);
        }

        private void SetHint(string msg)
        {
            if (hintText != null) hintText.text = msg;
        }
    }
}