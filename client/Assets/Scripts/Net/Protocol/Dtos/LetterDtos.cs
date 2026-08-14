using System;

namespace WildCourt.Net.Protocol
{
    // ---------- Letter（文档 02 §2.2 LetterController、07 §7.10） ----------

    [Serializable]
    public class LetterSendRequest
    {
        public string content;    // 手写 ≤50 字 / 模板 T01~T05
        public string itemId;     // 随附非遗（可为空）
    }

    [Serializable]
    public class LetterItem
    {
        public long letterId;
        public long fromUserId;
        public string fromNickname;
        public string title;
        public string content;
        public string itemId;     // rewardType=item 时的道具/非遗 ID
        public string rewardType; // shell/coupon/item/空
        public int rewardCount;   // shell/coupon 为货币数额，item 为背包数量
        public string status;     // 送达/已领取/已删除
        public long createdAt;
    }

    [Serializable]
    public class InboxResponse
    {
        public LetterItem[] letters;
    }

    [Serializable]
    public class GiftBackRequest
    {
        public long letterId;
        public string nonfictionId; // 回赠非遗（好友免审，文档 08 §8.5）
        public string content;
    }

    /// <summary>领取邮件随附道具/物品（收件方领取 itemId 附件）。</summary>
    [Serializable]
    public class ClaimLetterRequest
    {
        public long letterId;
    }

    [Serializable]
    public class ClaimResponse
    {
        public long letterId;
        public string rewardType; // shell/coupon/item/空
        public string itemId;     // rewardType=item 时领取到的道具/非遗
        public int count;         // 领取数量
    }

    [Serializable]
    public class LetterStatusResponse
    {
        public long letterId;
        public string status;
    }

    // ---------- Friend（FriendController、文档 08） ----------

    [Serializable]
    public class FriendApplyRequest
    {
        public long toUserId;
    }

    [Serializable]
    public class FriendAcceptRequest
    {
        public long fromUserId;
    }

    /// <summary>偷菜请求（文档 08 §8.3：30% 产量、1 次/作物/天、20 次/天、保护罩 24h）。</summary>
    [Serializable]
    public class StealRequest
    {
        public long targetUserId;
        public int cellId; // 目标农田格（0 起）
    }

    [Serializable]
    public class StealResultResponse
    {
        public string cropId;
        public int count;   // 偷取数量（floor(yield × 0.3)，受余量限制）
    }

    [Serializable]
    public class FeedRequest
    {
        public long targetUserId; // 喂食 3 次/天，宠物被喂 +2 EXP（文档 08 §8.4）
    }

    /// <summary>好友庭院快照（互访渲染，文档 03 §3.2 Yard）。</summary>
    [Serializable]
    public class YardSnapshotResponse
    {
        public long userId;
        public string nickname;
        public string layoutJson; // 庭院布局（摆放数据）
        public long foodCount;    // 食物盆余量（决定是否留下，文档 05 §5.3）
    }
}
