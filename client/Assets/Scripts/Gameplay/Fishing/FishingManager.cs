using System.Collections.Generic;
using UnityEngine;
using WildCourt.Core;

namespace WildCourt.Gameplay.Fishing
{
    /// <summary>
    /// 外部系统对接适配。项目当前 InventoryManager / CurrencyManager / PlayerLevel 为普通类
    /// （尚未做成单例），故以注入方式接入；接入真正的系统单例时只需调用
    /// <see cref="FishingManager.Configure"/> 传一次即可，逻辑不依赖具体实现。
    /// </summary>
    public interface IFishingContext
    {
        int PlayerLevel { get; }                          // ← 对接 WildCourt.Core.PlayerLevel.Level
        int GetBaitCount(string baitId);                  // ← InventoryManager.GetCount(itemId)
        bool ConsumeBait(string baitId);                  // ← InventoryManager.Remove(itemId, 1)
        void AddFish(string fishId);                      // ← InventoryManager.Add(itemId, 1)
        int GetFishCount(string fishId);                  // ← InventoryManager.GetCount(itemId)
        bool RemoveFish(string fishId, int count);        // ← InventoryManager.Remove(itemId, count)
        void AddShell(int amount);                        // ← CurrencyManager.AddShell(amount)
        bool OpenShopForBait(string baitId);              // 鱼饵不足时跳转商店（对接商店系统）
    }

    /// <summary>
    /// 默认上下文：直接操作项目现有核心类（InventoryManager/CurrencyManager/PlayerLevel）。
    /// 打开水井前需由外部（登录/庭院加载处）通过 <see cref="FishingManager.Configure"/> 注入真实实例。
    /// </summary>
    public class DefaultFishingContext : IFishingContext
    {
        public InventoryManager Inventory { get; set; }
        public CurrencyManager Currency { get; set; }
        public PlayerLevel Player { get; set; }
        public System.Action<string> ShopJump { get; set; }

        public int PlayerLevel => Player != null ? Player.Level : 1;

        public int GetBaitCount(string baitId) => Inventory != null ? Inventory.GetCount(baitId) : 0;

        public bool ConsumeBait(string baitId) => Inventory != null && Inventory.GetCount(baitId) > 0 && Inventory.Remove(baitId, 1);

        public void AddFish(string fishId)
        {
            if (Inventory != null) Inventory.Add(fishId, 1);
        }

        public int GetFishCount(string fishId) => Inventory != null ? Inventory.GetCount(fishId) : 0;

        public bool RemoveFish(string fishId, int count) => Inventory != null && Inventory.Remove(fishId, count);

        public void AddShell(int amount)
        {
            if (Currency != null) Currency.AddShell(amount);
        }

        public bool OpenShopForBait(string baitId)
        {
            ShopJump?.Invoke(baitId);
            return ShopJump != null;
        }
    }

    /// <summary>
    /// 钓鱼系统管理器（GameplayManager 单例，常驻）。
    /// <para>职责：解锁校验、鱼饵校验/扣除、FSM 驱动、咬钩调度、收线 QTE、结算写包、
    /// 中断（切后台/退出界面&gt;10s）判定。</para>
    /// <para>天气纯视觉不影响概率，本模块不做任何天气分支。</para>
    /// </summary>
    public class FishingManager : GameplayManager<FishingManager>
    {
        public const string WellBuildingId = "Well"; // 庭院「水井」建筑定位标识（与装饰/建筑配置对齐处）

        /// <summary>默认配置资源路径（Resources 下），未注入时兜底加载。</summary>
        public const string ConfigResourcePath = "Fishing/FishingConfig";

        [Header("配置")]
        [SerializeField] private FishingConfigSO config;

        /// <summary>外部系统注入（背包/货币/等级/商店）。</summary>
        public IFishingContext Context { get; private set; }

        /// <summary>状态机。</summary>
        public FishingStateMachine Fsm { get; } = new FishingStateMachine();

        /// <summary>当前选中的鱼饵ID（「再来一次」沿用它）。</summary>
        public string CurrentBaitId { get; private set; }

        /// <summary>收线进度（0~1）。</summary>
        public float ReelProgress { get; private set; }

        /// <summary>等待阶段剩余秒（倒计时展示）。</summary>
        public float RemainingSeconds { get; private set; }

        public FishingConfigSO Config => CurrentConfig;

        // ---- 对外事件（供 FishingPanel 订阅）----

        /// <summary>状态变更。</summary>
        public event System.Action<FishingState, FishingState> EStateChanged;

        /// <summary>等待阶段倒计时（剩余秒，每秒/每帧刷新）。</summary>
        public event System.Action<float> EWaitTick;

        /// <summary>咬钩前水花预警。</summary>
        public event System.Action ESplashWarning;

        /// <summary>触发咬钩（浮漂下沉 + 建议音效）。</summary>
        public event System.Action EBiteHit;

        /// <summary>收线进度变化（0~1）。</summary>
        public event System.Action<float> EReelProgress;

        /// <summary>收线脱钩（进度归零失败）。</summary>
        public event System.Action EReelFailure;

        /// <summary>鱼挣扎挣脱（拉力周期性下降 + 鱼图标位移，供 UI 同步）。</summary>
        public event System.Action EStruggle;

        /// <summary>结算结果。</summary>
        public event System.Action<FishingResult> EResult;

        /// <summary>鱼饵持有数量变化（选饵界面刷新）。</summary>
        public event System.Action EBaitCountChanged;

        /// <summary>背包鱼群变化（批量出售后刷新）。</summary>
        public event System.Action EFishInventoryChanged;

        private FishingConfigSO CurrentConfig;

        // ---- 活跃对局临时状态 ----
        private float _waitStartTime;
        private bool _biteWillHappen;      // 本次抛竿是否已判定咬钩
        private float _biteTime;           // 预排的咬钩时刻（相对 _waitStartTime）
        private bool _splashFired;
        private bool _hookedAwaitingTap;   // 已咬钩，等待「点击收线」
        private float _reelStartTime;
        private float _awayMark;           // 切后台时间戳（未激活时）
        private bool _awayActive;

        // ---- 周期性挣脱（鱼上钩后拉力会周期性下降）----
        [Header("收线·周期挣脱")]
        [Tooltip("鱼挣脱周期（秒），到点拉力下降一次")]
        [SerializeField] private float struggleInterval = 0.9f;
        [Tooltip("每次挣脱拉力下降量（0~1）")]
        [SerializeField] private float struggleAmount = 0.14f;
        private float _nextStruggleTime;   // 下次挣脱时刻

        protected override void Awake()
        {
            base.Awake();
            Fsm.OnStateChanged += OnFsmStateChanged;
            EnsureConfig();
            if (Context == null) Context = new DefaultFishingContext();
        }

        private void OnDestroy()
        {
            Fsm.OnStateChanged -= OnFsmStateChanged;
        }

        protected virtual void Update()
        {
            HandleInterruptionTick();
            switch (Fsm.Current)
            {
                case FishingState.Waiting:
                    TickWaiting();
                    break;
                case FishingState.Reeling:
                    TickReeling();
                    break;
            }
        }

        /// <summary>注入外部系统（配置与背包/货币/等级）。尚未配置时生成默认上下文。</summary>
        public void Configure(FishingConfigSO cfg, IFishingContext ctx)
        {
            config = cfg;
            Context = ctx ?? new DefaultFishingContext();
            EnsureConfig();
        }

        /// <summary>配置默认上下文持有的具体对象（快捷接入现有类实例）。</summary>
        public void ConfigureContext(InventoryManager inventory, CurrencyManager currency, PlayerLevel player, System.Action<string> shopJump = null)
        {
            var d = new DefaultFishingContext
            {
                Inventory = inventory,
                Currency = currency,
                Player = player,
                ShopJump = shopJump,
            };
            Context = d;
        }

        // ================= 入口 / 解锁校验 =================

        /// <summary>
        /// 水井入口：校验解锁等级。未达 2 级 → 弹「等级2级解锁钓鱼」提示（返回 false）。
        /// 已解锁 → 返回 true，交 UI 打开界面。
        /// </summary>
        public bool TryEnterWell(out int unlockLevel)
        {
            int lv = Context != null ? Context.PlayerLevel : 1;
            unlockLevel = CurrentConfig != null ? CurrentConfig.unlockLevel : 2;
            return lv >= unlockLevel;
        }

        /// <summary>便捷封装：返回解锁等级。</summary>
        public int GetUnlockLevel() => CurrentConfig != null ? CurrentConfig.unlockLevel : 2;

        // ================= 选饵 / 抛竿 =================

        /// <summary>选中鱼饵（选饵阶段高亮）。</summary>
        public void SelectBait(string baitId)
        {
            if (Fsm.Current != FishingState.Idle) return;
            if (CurrentConfig.GetBait(baitId) == null) return;
            CurrentBaitId = baitId;
        }

        /// <summary>
        /// 抛竿：立即扣 1 个鱼饵并启动 60s 生效倒计时。
        /// <para>扣饵成功即开始对局：判定本次咬钩是否发生 + 预排咬钩时刻。
        /// 扣饵失败（持有不足）返回 false。</para>
        /// </summary>
        public bool Cast(string baitId)
        {
            if (Fsm.Current != FishingState.Idle) return false;
            var bait = CurrentConfig.GetBait(baitId);
            if (bait == null) return false;

            bool consumed = Context != null && Context.ConsumeBait(baitId);
            if (!consumed)
            {
                Debug.LogWarning($"[Fishing] 鱼饵 {baitId} 数量不足，抛竿失败");
                return false;
            }

            CurrentBaitId = baitId;
            _biteWillHappen = FishingMath.RollBite(bait);
            // 预排咬钩时刻：在 [minBiteDelay, waitSeconds) 内随机。未咬钩则 _biteTime 无意义。
            _biteTime = _biteWillHappen
                ? Random.Range(CurrentConfig.minBiteDelaySeconds, CurrentConfig.waitSeconds)
                : float.MaxValue;
            _splashFired = false;
            _hookedAwaitingTap = false;
            _waitStartTime = Time.unscaledTime;
            RemainingSeconds = CurrentConfig.waitSeconds;
            ReelProgress = 0f;

            Fsm.TryChange(FishingState.Casting);
            // 抛竿入水动画为瞬时，直接进入等待阶段（表现层可在 Casting 插帧）。
            Fsm.TryChange(FishingState.Waiting);
            EBaitCountChanged?.Invoke();
            return true;
        }

        /// <summary>「再来一次」：沿用上一次选中的鱼饵。</summary>
        public bool CastAgain()
        {
            if (string.IsNullOrEmpty(CurrentBaitId)) return false;
            return Cast(CurrentBaitId);
        }

        // ================= 等待 / 咬钩 =================

        private void TickWaiting()
        {
            float elapsed = Time.unscaledTime - _waitStartTime;
            RemainingSeconds = Mathf.Max(0f, CurrentConfig.waitSeconds - elapsed);
            EWaitTick?.Invoke(RemainingSeconds);

            if (_biteWillHappen)
            {
                // 咬钩前 splashWarningSeconds 秒触发水花预警
                if (!_splashFired && elapsed >= _biteTime - CurrentConfig.splashWarningSeconds)
                {
                    _splashFired = true;
                    ESplashWarning?.Invoke();
                }
                // 到达预排咬钩时刻 → 进入咬钩
                if (elapsed >= _biteTime && !_hookedAwaitingTap)
                {
                    _hookedAwaitingTap = true;
                    EBiteHit?.Invoke();
                    Fsm.TryChange(FishingState.Hooked);
                    // 表现：等待玩家点击收线后 Fsm.TryChange(FishingState.Reeling)
                }
            }
            else if (RemainingSeconds <= 0f)
            {
                // 未咬钩倒计时结束 → 鱼饵失效，本次结束（无产出，不返还）
                Settle(new FishingResult(false, null, "鱼饵失效，未咬钩"));
            }
        }

        /// <summary>玩家「点击收线」：从咬钩进入收线阶段（鱼上钩挣扎状态）。</summary>
        public void StartReeling()
        {
            if (Fsm.Current != FishingState.Hooked) return;
            ReelProgress = 0f;
            _reelStartTime = Time.unscaledTime;
            _nextStruggleTime = Time.unscaledTime + struggleInterval;
            Fsm.TryChange(FishingState.Reeling);
            EReelProgress?.Invoke(ReelProgress);
        }

        // ================= 收线（连点充能 QTE）================

        /// <summary>连点充能：每次点击进度上涨 tapGain。</summary>
        public void RegisterReelTap()
        {
            if (Fsm.Current != FishingState.Reeling) return;
            ReelProgress = Mathf.Min(1f, ReelProgress + CurrentConfig.tapGain);
            EReelProgress?.Invoke(ReelProgress);
            if (ReelProgress >= 1f)
            {
                FinishReelingSuccess();
            }
        }

        private void TickReeling()
        {
            // 鱼周期性挣脱：到点拉力下降 struggleAmount，并通知 UI 鱼图标改变位置
            if (Time.unscaledTime >= _nextStruggleTime)
            {
                _nextStruggleTime = Time.unscaledTime + struggleInterval;
                ReelProgress = Mathf.Max(0f, ReelProgress - struggleAmount);
                EStruggle?.Invoke();
                EReelProgress?.Invoke(ReelProgress);
            }

            // 拉力归零 → 鱼逃跑，钓鱼失败
            if (ReelProgress <= 0f)
            {
                EReelFailure?.Invoke();
                Settle(new FishingResult(false, null, "拉力归零，鱼逃跑了"));
                return;
            }

            // 超过收线时长上限 → 判定失败（时长控制 3~8s）
            if (Time.unscaledTime - _reelStartTime > CurrentConfig.reelMaxSeconds)
            {
                Settle(new FishingResult(false, null, "收线超时"));
            }
        }

        private void FinishReelingSuccess()
        {
            // 出击鱼：按当前鱼饵出鱼权重抽取最终鱼种
            var bait = CurrentConfig.GetBait(CurrentBaitId);
            string fishId = FishingMath.WeightedFish(bait);
            if (fishId == null)
            {
                Settle(new FishingResult(false, null, "出鱼配置缺失"));
                return;
            }
            var fish = CurrentConfig.GetFish(fishId);
            if (fish == null)
            {
                Settle(new FishingResult(false, null, "鱼种配置缺失:" + fishId));
                return;
            }
            // 钓获鱼写包（同步背包，退出界面不丢失）
            if (Context != null) Context.AddFish(fishId);
            EFishInventoryChanged?.Invoke();
            Settle(new FishingResult(true, fish, "钓获：" + fish.name));
        }

        // ================= 结算 =================

        private void Settle(FishingResult result)
        {
            if (Fsm.Current == FishingState.Settlement) return;
            // 结算为终态，来源可能是 Waiting(超时)/Reeling(成功/脱钩/超时)/切后台，
            // 走 ForceAbort（任意活跃态直接收束到 Settlement，不校验常规转移表）。
            Fsm.ForceAbort();
            // 状态切换日志：钓鱼成功 / 钓鱼失败
            if (result.Success) Debug.Log($"[Fishing] 钓鱼成功：{result.Fish?.name ?? "?"}");
            else Debug.Log($"[Fishing] 钓鱼失败：{result.Reason}");
            // 数据上报：复用 NetManager，成功/失败均 POST 后端管理端接口
            FishingReporter.Report(result, CurrentBaitId);
            EResult?.Invoke(result);
            // 结算展示结束后回到待机（由 UI 调 ConfirmSettlement 或自动）。
        }

        /// <summary>结算确认完成 → 回待机（支持「再来一次」）。</summary>
        public void ConfirmSettlement()
        {
            if (Fsm.Current != FishingState.Settlement) return;
            Fsm.ResetToIdle();
            EBaitCountChanged?.Invoke();
        }

        // ================= 中断（切后台 / 退出界面 >10s）================

        private void HandleInterruptionTick()
        {
            if (!_awayActive || !Fsm.IsActive) return;
            if (Time.unscaledTime - _awayMark > CurrentConfig.interruptionThresholdSeconds)
            {
                _awayActive = false;
                // 中断：当前抛竿失效，鱼饵不返还，判定失败
                Settle(new FishingResult(false, null, "操作中断（切后台时间过长）"));
            }
        }

        /// <summary>UI 调用：界面隐藏/切后台时打点；断线中断同走此路径。</summary>
        public void MarkInterrupted()
        {
            if (!Fsm.IsActive) return;
            _awayActive = true;
            _awayMark = Time.unscaledTime;
        }

        /// <summary>UI 调用：回到前台/界面恢复，清除打点（离开超过阈值由 Update 判定）。</summary>
        public void CancelInterruption()
        {
            _awayActive = false;
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) MarkInterrupted();
            else CancelInterruption();
        }

        // ================= 批量出售（鱼类 → 庭贝）================

        /// <summary>
        /// 批量出售鱼群：按 fishId 逐一出售，实时累加庭贝收益，写回背包并加币。
        /// sortedFish = (fishId, count)。金鱼(不可食用)同理可直接出售。
        /// </summary>
        public int SellFishes(IReadOnlyList<(string fishId, int count)> batch)
        {
            if (batch == null || Context == null) return 0;
            int totalShell = 0;
            foreach (var (fishId, count) in batch)
            {
                var fish = CurrentConfig.GetFish(fishId);
                if (fish == null || count <= 0) continue;
                int available = Context.GetFishCount(fishId);
                int sell = Mathf.Min(count, available);
                if (sell <= 0 || !Context.RemoveFish(fishId, sell)) continue;
                totalShell += fish.sellPrice * sell;
                EFishInventoryChanged?.Invoke();
            }
            if (totalShell > 0)
            {
                Context.AddShell(totalShell);
            }
            return totalShell;
        }

        /// <summary>出售全部某鱼种，返回收入。</summary>
        public int SellAllOf(string fishId)
        {
            if (Context == null) return 0;
            int count = Context.GetFishCount(fishId);
            if (count <= 0) return 0;
            return SellFishes(new[] { (fishId, count) });
        }

        // ================= 只读查询 =================

        public int GetBaitCount(string baitId) => Context != null ? Context.GetBaitCount(baitId) : 0;
        public int GetFishCount(string fishId) => Context != null ? Context.GetFishCount(fishId) : 0;

        /// <summary>通知鱼饵持有量已变化（供外部模拟购买/赠送后触发 UI 刷新）。</summary>
        public void NotifyBaitCountChanged() => EBaitCountChanged?.Invoke();

        /// <summary>通知背包鱼群已变化（供外部同步后触发 UI 刷新）。</summary>
        public void NotifyFishInventoryChanged() => EFishInventoryChanged?.Invoke();

        /// <summary>鱼饵配置列表（UI 选饵界面）。</summary>
        public IReadOnlyList<BaitData> Baits => CurrentConfig.baits;

        /// <summary>鱼类配置列表。</summary>
        public IReadOnlyList<FishData> Fishes => CurrentConfig.fishes;

        public BaitData GetBait(string baitId) => CurrentConfig.GetBait(baitId);
        public FishData GetFish(string fishId) => CurrentConfig.GetFish(fishId);

        private void EnsureConfig()
        {
            if (config == null)
            {
                config = Resources.Load<FishingConfigSO>(ConfigResourcePath);
            }
            if (config == null)
            {
                config = FishingConfigSO.CreateDefault();
                Debug.LogWarning("[Fishing] 未注入/未找到 FishingConfig 资源，已使用文档默认数值（建议在根部创建配置资产注入）");
            }
            CurrentConfig = config;
        }

        private void OnFsmStateChanged(FishingState from, FishingState to)
        {
            // 每个状态切换打印控制台日志（等待咬钩/鱼上钩挣扎/钓鱼成功/钓鱼失败）
            Debug.Log($"[Fishing] 状态切换：{from} → {to}");
            EStateChanged?.Invoke(from, to);
        }
    }

    // ---- 桥接到 WildCourt.Core 层的便捷静态入口（可选）----
    // 若项目其他地方（如庭院水井点击）想不持有引用地触发，可经 EventBus 广播：
    //   EventBus.Publish(new WildCourt.Core.PlayerExpChangedEvent { ... });
    // FishingManager 不依赖该事件，仅保留此接口说明，避免耦合。
}