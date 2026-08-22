using System;
using UnityEngine;

namespace WildCourt.Gameplay.Fishing
{
    /// <summary>
    /// 钓鱼有限状态机（FSM），管理「待机-抛竿-等待-咬钩-收线-结算-结束」全流程。
    /// <para>纯状态管理，不持有任何 UI；合法转移表见 <see cref="CanTransition"/>。
    /// 状态变更经 <see cref="OnStateChanged"/> 对外广播，供 <see cref="FishingManager"/> 驱动表现。</para>
    /// </summary>
    public class FishingStateMachine
    {
        /// <summary>当前状态。</summary>
        public FishingState Current { get; private set; } = FishingState.Idle;

        /// <summary>状态变更事件（old, new）。</summary>
        public event Action<FishingState, FishingState> OnStateChanged;

        /// <summary>
        /// 转移合法性检查。仅允许文档定义的状态链：
        /// <para>Idle → Casting（抛竿）；Casting → Waiting（入水完成）；
        /// Waiting → Hooked（咬钩）；Hooked → Reeling（点击收线）；
        /// Reeling → Settlement（收线完成/脱钩收束）；Settlement → Idle（结算结束）。</para>
        /// 异常中断（切后台/超时）由管理器经 <see cref="ForceAbort"/> 直接跳 Settlement。
        /// </summary>
        public bool CanTransition(FishingState from, FishingState to)
        {
            switch (from)
            {
                case FishingState.Idle: return to == FishingState.Casting;
                case FishingState.Casting: return to == FishingState.Waiting;
                case FishingState.Waiting: return to == FishingState.Hooked;
                case FishingState.Hooked: return to == FishingState.Reeling;
                case FishingState.Reeling: return to == FishingState.Settlement;
                case FishingState.Settlement: return to == FishingState.Idle;
                default: return false;
            }
        }

        /// <summary>尝试切换到目标状态（校验合法性）。成功返回 true。</summary>
        public bool TryChange(FishingState to)
        {
            return TryChangeTo(Current, to);
        }

        private bool TryChangeTo(FishingState from, FishingState to)
        {
            if (!CanTransition(from, to))
            {
                Debug.LogWarning($"[FishingFSM] 非法状态转移 {from} -> {to} 已忽略");
                return false;
            }
            SetState(to);
            return true;
        }

        /// <summary>强制设置状态（仅在 as 兜底），当状态已满足目标时不触发事件。</summary>
        private void SetState(FishingState to)
        {
            if (Current == to) return;
            FishingState old = Current;
            Current = to;
            OnStateChanged?.Invoke(old, Current);
        }

        /// <summary>
        /// 异常中断：切后台/退出界面/超时等异常场景统一判定为钓鱼失败。
        /// 不校验合法性（允许任意状态 → Settlement），保证异常时一定收束到结算。
        /// </summary>
        public bool ForceAbort()
        {
            if (Current == FishingState.Idle || Current == FishingState.Settlement) return false;
            FishingState old = Current;
            Current = FishingState.Settlement;
            OnStateChanged?.Invoke(old, Current);
            return true;
        }

        /// <summary>是否处于投竿后的活跃流程（抛竿~收线），用于判定中断是否有效。</summary>
        public bool IsActive => Current == FishingState.Casting
                                || Current == FishingState.Waiting
                                || Current == FishingState.Hooked
                                || Current == FishingState.Reeling;

        /// <summary>直接强制回到待机并广播（结算完成回到待机，支持「再来一次」）。</summary>
        public void ResetToIdle()
        {
            if (Current == FishingState.Idle) return;
            FishingState old = Current;
            Current = FishingState.Idle;
            OnStateChanged?.Invoke(old, Current);
        }
    }
}