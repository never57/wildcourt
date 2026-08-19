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
    public class LetterAttachmentDto
    {
        public string type;     // shell/coupon/item
        public string itemId;   // type=item 时的道具/非遗 ID
        public int count;       // 数量
    }

    [Serializable]
    public class LetterItem
    {
        public long letterId;
        public long fromUserId;
        public string fromNickname;
        public string title;
        public string content;
        public LetterAttachmentDto[] attachments; // 附件列表（多附件），空 = 无附件
        public string status;     // 未读/已读/已领取/已删除
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
        public LetterAttachmentDto[] attachments; // 领取到的附件列表（多附件）
    }

    [Serializable]
    public class LetterStatusResponse
    {
        public long letterId;
        public string status;
    }
}
