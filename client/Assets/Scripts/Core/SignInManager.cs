using UnityEngine;

namespace WildCourt.Core
{
    /// <summary>
    /// 签到本地状态（文档 11 §11.7）：只管 UI 与本地缓存；
    /// 签到奖励发放走服务端每日结算链路（SettlementService），
    /// 客户端不拆分结算逻辑，仅调用签到读写接口刷新展示。
    /// </summary>
    public class SignInManager : MonoBehaviour
    {
        public static SignInManager Instance { get; private set; }

        /// <summary>今日是否已签到（本地缓存，仅展示）。</summary>
        public bool SignedToday { get; private set; }

        /// <summary>连续签到天数（本地缓存，仅展示）。</summary>
        public int ConsecutiveDays { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 从服务端刷新签到状态：GET /api/v1/signin/today（文档 02 §2.2 DailySignInController）。
        /// TODO：接入 NetManager 后实现，解析响应并广播 UI 刷新事件。
        /// </summary>
        public void RefreshFromServer()
        {
            // WildCourt.Net.NetManager.Instance.Get("/api/v1/signin/today",
            //     resp => { SignedToday = resp.data.signedToday; ConsecutiveDays = resp.data.days; });
        }

        /// <summary>领取周/月签到奖励（POST /api/v1/signin/claim-week | claim-month）。</summary>
        public void ClaimWeek() { /* TODO: NetManager.Post 接入 */ }

        public void ClaimMonth() { /* TODO: NetManager.Post 接入 */ }
    }
}
