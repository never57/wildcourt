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
    }

    /// <summary>历史消息响应。</summary>
    [Serializable]
    public class ChatHistoryResponse
    {
        public ChatMessageDto[] messages;
    }
}
