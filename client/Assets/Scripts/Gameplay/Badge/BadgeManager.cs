using UnityEngine;

namespace WildCourt.Gameplay.Badge
{
    /// <summary>
    /// 图鉴与徽章（文档 03 §3.2 Badge/、12）：34 省非遗图鉴（仅「收到异地玩家赠送」解锁）、
    /// 徽章护照（5/10/15/20/25/30/33/34 省里程碑，文档 05 §5.1）。数据服务端权威，本地缓存展示。
    /// </summary>
    public class BadgeManager : GameplayManager<BadgeManager>
    {
        /// <summary>图鉴里程碑（收集省份数 → 奖励档位，文档 12）。</summary>
        public static readonly int[] Milestones = { 5, 10, 15, 20, 25, 30, 33, 34 };

        // TODO(文档 12)：图鉴收集进度（Config_Badges + 服务端 nonfiction_collections 快照）、徽章护照解锁动效

        /// <summary>本地新增图鉴解锁（由 LetterArrived/徽章推送触发）。</summary>
        public void OnProvinceUnlocked(int provinceId)
        {
            Debug.Log($"[BadgeManager] 解锁省份图鉴 {provinceId}");
            // TODO: 广播 BadgeUnlockedEvent，检查里程碑
        }
    }
}
