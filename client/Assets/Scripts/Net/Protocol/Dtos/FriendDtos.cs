using System;

namespace WildCourt.Net.Protocol
{
    /// <summary>好友信息（与 FriendController /friend/list 响应对齐）。</summary>
    [Serializable]
    public class FriendItem
    {
        public long userId;
        public string nickname;
        public string avatarUrl;
        public int level;
        public int unreadCount;  // 该好友发给我的未读消息数（未读角标用）
    }

    /// <summary>好友列表响应。</summary>
    [Serializable]
    public class FriendListResponse
    {
        public FriendItem[] friends;
    }

    /// <summary>好友申请记录。</summary>
    [Serializable]
    public class FriendRequestItem
    {
        public long id;
        public long fromUserId;
        public string fromNickname;
        public string avatarUrl;
        public string signature;
        public string status;
        public long createdAt;
    }

    /// <summary>好友申请列表响应。</summary>
    [Serializable]
    public class FriendRequestListResponse
    {
        public FriendRequestItem[] requests;
    }

    /// <summary>发送私聊消息请求。</summary>
    [Serializable]
    public class SendChatRequest
    {
        public long toUserId;
        public string content;
    }

    /// <summary>私聊消息（与 ChatController 响应 / ChatHub ReceivePrivateMessage 对齐）。</summary>
    [Serializable]
    public class ChatMessageDto
    {
        public long id;
        public long fromUserId;
        public long toUserId;
        public string content;
        public bool isRead;
        public long createdAt;
        public string avatarUrl;
    }

    /// <summary>历史消息响应。</summary>
    [Serializable]
    public class ChatHistoryResponse
    {
        public ChatMessageDto[] messages;
    }

    /// <summary>好友申请/接受/拒绝请求体（apply 时 userId=被申请人，accept/reject 时 userId=申请人）。</summary>
    [Serializable]
    public class FriendApplyRequest
    {
        public long userId;
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
