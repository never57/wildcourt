using System;
using UnityEngine;
using WildCourt.Core;

namespace WildCourt.Gameplay.DailySettlement
{
    /// <summary>
    /// 每日结算（文档 03 §3.2 DailySettlement/、14 §14.5）：04:00 结算播报展示、
    /// 推送订阅（SignalR DailySettlement 事件）。结算逻辑全部在服务端（SettlementService），
    /// 客户端仅做播报与本地展示。
    /// </summary>
    public class DailySettlementManager : GameplayManager<DailySettlementManager>
    {
        private void OnEnable()
        {
            EventBus.Subscribe<DailySettlementEvent>(OnSettlementPushed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DailySettlementEvent>(OnSettlementPushed);
        }

        /// <summary>04:00 结算播报（文档 14 §14.5：「早安！你的院子里有...」）。</summary>
        private void OnSettlementPushed(DailySettlementEvent evt)
        {
            Debug.Log($"[DailySettlementManager] 结算播报: {evt.Report}");
            // TODO: 结算弹窗/公告展示；宠物状态恢复（派遣中→休息中→在家）表现刷新
        }

        /// <summary>上次结算时刻（本地展示用，服务端 UTC+8 04:00）。</summary>
        public DateTime LastSettlementTime()
        {
            return WildCourt.Utils.TimeUtil.LastSettlementTime(WildCourt.Utils.TimeUtil.NowServerTime());
        }
    }
}
