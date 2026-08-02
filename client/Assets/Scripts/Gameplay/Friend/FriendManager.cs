using UnityEngine;

namespace WildCourt.Gameplay.Friend
{
    /// <summary>
    /// 好友（文档 03 §3.2 Friend/、08）：好友列表、偷菜、喂食、互访入口。
    /// 偷菜 30% 产量、1 次/作物/天、20 次/天、保护罩 24h；喂食 3 次/天、被喂 +2 EXP（上限 +10/天）。
    /// 互访庭院渲染见 YardManager。好友加成：好客值 +50 / 匹配 +10% / 回赠免审（文档 05 §5.5）。
    /// </summary>
    public class FriendManager : GameplayManager<FriendManager>
    {
        // TODO(文档 08)：好友列表缓存、申请/接受流程、偷菜与喂食次数本地展示（服务端权威）

        /// <summary>偷菜入口（POST /api/v1/friend/steal）。</summary>
        public void Steal(long targetUserId, string cropCellId)
        {
            Debug.Log($"[FriendManager] 偷菜 {targetUserId}.{cropCellId}");
            // NetManager.Instance.Post<StealResultResponse>(ApiPaths.FriendSteal,
            //     new StealRequest { targetUserId = targetUserId, cropCellId = cropCellId },
            //     resp => { /* 30% 概率判定在服务端 */ });
        }

        /// <summary>喂食入口（POST /api/v1/friend/feed，3 次/天）。</summary>
        public void Feed(long targetUserId)
        {
            Debug.Log($"[FriendManager] 喂食 {targetUserId}");
        }
    }
}
