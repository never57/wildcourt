namespace WildCourt.Api.Models;

// ============ 偷菜 ============

/// <summary>偷菜请求（POST /api/v1/friend/steal）。</summary>
public class StealCropRequest
{
    public long TargetUserId { get; set; }

    /// <summary>目标农田格（0 起）。</summary>
    public int CellId { get; set; }
}

/// <summary>偷菜结果（count = floor(yield × stealRatio)，受余量限制）。</summary>
public class StealResultDto
{
    public long TargetUserId { get; set; }

    public string CropId { get; set; } = string.Empty;

    public int Count { get; set; }
}

// ============ 好友申请/接受 ============

/// <summary>好友申请/接受请求。</summary>
public class FriendRequest
{
    /// <summary>目标玩家 ID（申请：被申请人；接受：申请人）。</summary>
    public long UserId { get; set; }
}

/// <summary>好友信息。</summary>
public class FriendDto
{
    public long UserId { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public string AvatarUrl { get; set; } = string.Empty;

    public int Level { get; set; }
}

/// <summary>好友申请记录（被申请人视角的待处理申请）。</summary>
public class FriendRequestDto
{
    public long Id { get; set; }

    public long FromUserId { get; set; }

    public string FromNickname { get; set; } = string.Empty;

    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>申请人个性标签（未设置时为空，客户端显示默认文案）。</summary>
    public string? Signature { get; set; }

    public string Status { get; set; } = string.Empty;

    public long CreatedAt { get; set; }
}

/// <summary>好友列表响应。</summary>
public class FriendListResponse
{
    public List<FriendDto> Friends { get; set; } = new();
}

/// <summary>好友申请列表响应。</summary>
public class FriendRequestListResponse
{
    public List<FriendRequestDto> Requests { get; set; } = new();
}

// ============ 私聊 ============

/// <summary>发送私聊消息请求。</summary>
public class SendChatRequest
{
    public long ToUserId { get; set; }

    public string Content { get; set; } = string.Empty;
}

/// <summary>聊天消息（响应契约，与客户端 ChatMessageDto 对齐）。</summary>
public class ChatMessageDto
{
    public long Id { get; set; }

    public long FromUserId { get; set; }

    public long ToUserId { get; set; }

    public string Content { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public long CreatedAt { get; set; }

    /// <summary>发送者头像 URL（服务端按 FromUserId 关联 users 表回填；可为空）。</summary>
    public string AvatarUrl { get; set; } = string.Empty;
}

/// <summary>历史消息响应。</summary>
public class ChatHistoryResponse
{
    public List<ChatMessageDto> Messages { get; set; } = new();
}
