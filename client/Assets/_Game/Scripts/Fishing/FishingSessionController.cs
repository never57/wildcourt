using System.Collections;
using Game.Data;
using Game.Services;
using Game.UI;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>
    /// 钓鱼会话状态机（收线全流程）：
    /// Idle → Waiting(60s) → Biting(上钩提示) → Reeling(连点收线) → Success(鱼入背包)/Failed(超时逃脱)。
    /// 咬钩概率来自鱼饵配置 biteChance；满进度后按鱼饵 catchWeights 权重随机鱼种。
    /// </summary>
    public enum FishingState { Idle, Waiting, Biting, Reeling, Success, Failed }

    public class FishingSessionController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] FishingDataTable dataTable;   // 鱼种 → FishConfig 映射用
        [SerializeField] FishingRodView rodView;
        [SerializeField] FishingLineView lineView;
        [SerializeField] FishingMainButtonView mainButton;
        [SerializeField] TensionPanelController tensionPanel;  // 收线面板
        [SerializeField] GameObject bubblePrefab;   // PF_Bubble_Wait
        [SerializeField] GameObject floatPrefab;    // PF_FloatIcon
        [SerializeField] RectTransform castPoint;   // 落点锚点

        [Header("收线流程参数")]
        [SerializeField] float waitSeconds = 60f;           // 咬钩等待期（秒）
        [SerializeField] float reelIncrementPercent = 20f;  // 每次点击收线增量（%），可配置
        [SerializeField] float reelTimeoutSeconds = 1.5f;   // 连点超时（秒），超时进度归零并逃脱

        public FishingState State { get; private set; } = FishingState.Idle;
        public FishConfig CurrentFish { get; private set; }
        public BaitConfig CurrentBait { get; private set; }

        public event System.Action<FishingState> OnStateChanged;
        public event System.Action<FishConfig> OnFishHooked;   // 咬钩（鱼种未定，回调参数为 null）
        public event System.Action<FishConfig> OnFishCaught;   // 成功钓获
        public event System.Action OnFishEscaped;              // 超时逃脱
        public event System.Action OnLineBroken;               // 保留：新流程不再触发

        /// <summary>收线进度 0~100。</summary>
        public float CatchProgress => _catchProgress;
        /// <summary>当前已连点次数。</summary>
        public int ReelClickCount => _reelClicks;
        /// <summary>满进度所需点击次数（100 / 单次增量）。</summary>
        public int RequiredReelClicks => Mathf.Max(1, Mathf.CeilToInt(100f / Mathf.Max(0.1f, reelIncrementPercent)));
        public TensionPanelController TensionPanel => tensionPanel;
        public FishingBubbleView BubbleView => _bubble;
        public FishingFloatView FloatView => _float;
        public FishingLineView LineView => lineView;
        public FishingMainButtonView MainButton => mainButton;

        private FishingBubbleView _bubble;
        private FishingFloatView _float;
        private Coroutine _flow;

        // ===== 收线数据 =====
        private float _catchProgress;       // 0~100
        private int _reelClicks;            // 已连点次数
        private float _lastReelClickTime;   // 最近一次点击时间（超时判定）

        private void Awake()
        {
            // 气泡 / 浮漂挂在 castPoint（落点）下，浮动动画相对落点
            Transform bubbleParent = castPoint != null ? castPoint : transform;
            if (bubblePrefab != null)
            {
                _bubble = Instantiate(bubblePrefab, bubbleParent).GetComponent<FishingBubbleView>();
                if (_bubble != null) _bubble.Hide();
            }
            if (floatPrefab != null)
            {
                _float = Instantiate(floatPrefab, bubbleParent).GetComponent<FishingFloatView>();
                if (_float != null) _float.Hide();
            }
            if (lineView != null)
            {
                lineView.SetRodTip(rodView != null ? rodView.RodTip : null);
                lineView.SetFloatAnchor(_float != null ? _float.transform as RectTransform : null);
                lineView.Hide();
            }
            mainButton?.BindController(this);
        }

        private void Update()
        {
            if (State == FishingState.Waiting || State == FishingState.Biting || State == FishingState.Reeling)
                lineView?.Refresh();

            // 连点超时判定：仅 Reeling 状态判定“两次点击间隔”超时。
            // Biting 不超时：等待玩家首次点击开始收线（避免 60s 等待后 1.5s 反应窗口导致“钓不到鱼”）。
            if (State == FishingState.Reeling
                && Time.time - _lastReelClickTime > reelTimeoutSeconds)
            {
                HandleReelTimeout();
            }
        }

        /// <summary>主按钮统一路由：Idle 抛竿；Biting/Reeling 连点收线。</summary>
        public void HandleMainButtonClick()
        {
            switch (State)
            {
                case FishingState.Idle: TryStartFishing(); break;
                case FishingState.Biting:
                case FishingState.Reeling:
                    ApplyReelClick();
                    break;
            }
        }

        /// <summary>仅 Idle 响应抛竿：消耗 1 鱼饵 → 进入 Waiting 等待期。</summary>
        public void TryStartFishing()
        {
            if (State != FishingState.Idle) return;
            var svc = InventoryService.Instance;
            var bait = svc.SelectedBait;
            if (bait == null || svc.GetCount(bait.id) <= 0)
            {
                ToastManager.Instance?.Show("鱼饵不足，请前往商店");
                UICommands.HighlightShop();
                return;
            }
            if (!svc.TryRemoveItem(bait.id, 1)) return;

            CurrentBait = bait;
            CurrentFish = null;
            _catchProgress = 0f;
            _reelClicks = 0;
            _flow = StartCoroutine(WaitForBiteFlow(bait));
        }

        /// <summary>Waiting 流程：等待 waitSeconds → 按鱼饵咬钩概率判定。</summary>
        private IEnumerator WaitForBiteFlow(BaitConfig bait)
        {
            // Idle → Waiting：甩竿 + 浮漂落点 + 鱼线
            SetState(FishingState.Waiting);
            rodView?.PlayCast();
            _float?.Show();
            lineView?.Show();
            _bubble?.SetBubble("等待鱼儿上钩…", false);

            // 1 分钟（可配置）咬钩等待期；下限保护防止误配 0 导致等待流程被提前终止
            yield return new WaitForSeconds(Mathf.Max(0.1f, waitSeconds));

            // 咬钩判定：按鱼饵 biteChance（Unity Random，无固定种子）
            bool bite = Random.value < bait.biteChance;
            if (!bite)
            {
                // 未咬钩：自动结束，鱼饵不返还
                ToastManager.Instance?.Show("鱼儿没有咬钩…");
                yield return new WaitForSeconds(0.8f);
                ResetToIdle();
                yield break;
            }

            // 咬钩成功 → Biting：暂停自动流程，显示上钩提示 + 收线面板
            SetState(FishingState.Biting);
            _catchProgress = 0f;
            _reelClicks = 0;
            _lastReelClickTime = Time.time;   // 上钩即开始 1.5s 计时
            _bubble?.SetBubble("上钩了！快收线！", true);
            tensionPanel?.Show(null);
            OnFishHooked?.Invoke(null);
            yield break;   // Biting/Reeling 由按钮点击驱动，Update 负责超时
        }

        /// <summary>连点收线：每次固定增量，满 100% 按权重抽鱼入背包。</summary>
        private void ApplyReelClick()
        {
            if (State != FishingState.Biting && State != FishingState.Reeling) return;
            if (State == FishingState.Biting) SetState(FishingState.Reeling);

            _reelClicks++;
            _lastReelClickTime = Time.time;
            _catchProgress = Mathf.Min(100f, _catchProgress + reelIncrementPercent);

            tensionPanel?.SetCatchProgress(_catchProgress);
            tensionPanel?.SetReelCounter(_reelClicks, RequiredReelClicks);
            mainButton?.PlayClickPunch();

            if (_catchProgress >= 100f)
            {
                _flow = StartCoroutine(CompleteReelingFlow());
            }
        }

        /// <summary>进度满：按当前鱼饵 catchWeights 权重随机鱼种 → 入背包 → Success → Idle。</summary>
        private IEnumerator CompleteReelingFlow()
        {
            SetState(FishingState.Success);

            // 权重抽鱼（纯函数，不依赖 UnityEngine.Random）
            FishSpecies species = CurrentBait != null
                ? CurrentBait.RollFish()
                : (dataTable != null && dataTable.fishes.Length > 0 ? dataTable.fishes[0].species : FishSpecies.Crucian);
            CurrentFish = FindFish(species);

            if (CurrentFish != null)
            {
                InventoryService.Instance.AddItem(CurrentFish.id, 1);
                CreelService.Instance.Add(CurrentFish);
                ToastManager.Instance?.Show($"钓到了【{CurrentFish.displayName}】！");
                OnFishCaught?.Invoke(CurrentFish);
            }
            tensionPanel?.PlayResult(ResultKind.Success);
            _bubble?.SetBubble("收线成功！", false);
            yield return new WaitForSeconds(1f);
            ResetToIdle();
        }

        /// <summary>连点超时：进度归零 + 逃脱提示 + Failed → Idle。</summary>
        private void HandleReelTimeout()
        {
            _catchProgress = 0f;
            _reelClicks = 0;
            tensionPanel?.SetCatchProgress(0f);
            tensionPanel?.SetReelCounter(0, RequiredReelClicks);
            ToastManager.Instance?.Show("收线中断，鱼已逃脱");
            OnFishEscaped?.Invoke();
            _bubble?.SetBubble("收线中断，鱼已逃脱", false);
            tensionPanel?.PlayResult(ResultKind.Escaped);
            SetState(FishingState.Failed);
            if (_flow != null) { StopCoroutine(_flow); _flow = null; }
            StartCoroutine(DelayedResetToIdle(1f));
        }

        private IEnumerator DelayedResetToIdle(float delay)
        {
            yield return new WaitForSeconds(delay);
            ResetToIdle();
        }

        private void SetState(FishingState state)
        {
            if (State == state) return;
            State = state;
            mainButton?.SetState(state);
            OnStateChanged?.Invoke(state);
        }

        private FishConfig FindFish(FishSpecies species)
        {
            if (dataTable == null || dataTable.fishes == null) return null;
            for (int i = 0; i < dataTable.fishes.Length; i++)
                if (dataTable.fishes[i] != null && dataTable.fishes[i].species == species)
                    return dataTable.fishes[i];
            return null;
        }

        private void ResetToIdle()
        {
            if (_flow != null) { StopCoroutine(_flow); _flow = null; }
            _bubble?.Hide();
            _float?.Hide();
            lineView?.Hide();
            tensionPanel?.Hide();
            CurrentFish = null;
            CurrentBait = null;
            _catchProgress = 0f;
            _reelClicks = 0;
            SetState(FishingState.Idle);
        }
    }
}
