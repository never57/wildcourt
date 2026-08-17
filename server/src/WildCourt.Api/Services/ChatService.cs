using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Hubs;
using WildCourt.Api.Models;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>好友私聊：发送（入库 + 实时推送）、历史记录、标记已读。</summary>
public class ChatService
{
    private readonly WildCourtDbContext _db;
    private readonly RealtimeConnectionManager _realtime;

    public ChatService(WildCourtDbContext db, RealtimeConnectionManager realtime)
    {
        _db = db;
        _realtime = realtime;
    }

    /// <summary>校验好友关系。</summary>
    private async Task EnsureFriendAsync(long userId, long otherUserId)
    {
        var isFriend = await _db.Friends.AnyAsync(f =>
            (f.UserId == userId && f.FriendId == otherUserId) ||
            (f.UserId == otherUserId && f.FriendId == userId));
        if (!isFriend)
        {
            throw new BusinessException(ApiCodes.BadRequest, "还不是好友，无法私聊");
        }
    }

    /// <summary>发送私聊消息：入库并返回消息 DTO（实时推送由 ChatHub 负责）。</summary>
    public async Task<ChatMessageDto> SendAsync(long fromUserId, long toUserId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new BusinessException(ApiCodes.BadRequest, "消息内容不能为空");
        }
        if (content.Length > 1000)
        {
            throw new BusinessException(ApiCodes.BadRequest, "消息过长（最多 1000 字）");
        }
        if (fromUserId == toUserId)
        {
            throw new BusinessException(ApiCodes.BadRequest, "不能给自己发消息");
        }
        var targetExists = await _db.Users.AnyAsync(u => u.UserId == toUserId);
        if (!targetExists)
        {
            throw new BusinessException(ApiCodes.NotFound, "目标玩家不存在");
        }
        await EnsureFriendAsync(fromUserId, toUserId);

        var entity = new ChatMessageEntity
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Content = content.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        };
        _db.ChatMessages.Add(entity);
        await _db.SaveChangesAsync();

        var dto = await ToDtoAsync(entity);

        // 实时推送给接收者（在线则立刻收到；离线消息由历史记录接口拉取）
        await _realtime.SendAsync(toUserId, "ReceivePrivateMessage", dto);

        return dto;
    }

    /// <summary>与某好友的历史消息（双向），按时间升序；同时把对方发给我的未读消息标记已读。</summary>
    public async Task<ChatHistoryResponse> HistoryAsync(long userId, long friendUserId, long? beforeId = null, int limit = 50)
    {
        await EnsureFriendAsync(userId, friendUserId);

        var query = _db.ChatMessages.Where(m =>
            (m.FromUserId == userId && m.ToUserId == friendUserId) ||
            (m.FromUserId == friendUserId && m.ToUserId == userId));

        if (beforeId.HasValue && beforeId.Value > 0)
        {
            query = query.Where(m => m.Id < beforeId.Value);
        }

        var messages = await query
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .OrderBy(m => m.Id)
            .ToListAsync();

        // 标记对方发给我的未读消息为已读
        var unread = messages.Where(m => m.ToUserId == userId && !m.IsRead).ToList();
        if (unread.Count > 0)
        {
            foreach (var m in unread)
            {
                m.IsRead = true;
            }
            await _db.SaveChangesAsync();
        }

        // 批量查询发送者头像，避免逐条消息 N+1 查询
        var fromIds = messages.Select(m => m.FromUserId).Distinct().ToList();
        var avatarMap = await _db.Users
            .Where(u => fromIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.AvatarUrl ?? string.Empty);

        return new ChatHistoryResponse
        {
            Messages = messages.Select(m => ToDto(m, avatarMap)).ToList(),
        };
    }

    /// <summary>构造消息 DTO，并从头像字典回填发送者头像。</summary>
    private static ChatMessageDto ToDto(ChatMessageEntity e, IReadOnlyDictionary<long, string> avatarMap)
    {
        avatarMap.TryGetValue(e.FromUserId, out var avatarUrl);
        return new ChatMessageDto
        {
            Id = e.Id,
            FromUserId = e.FromUserId,
            ToUserId = e.ToUserId,
            Content = e.Content,
            IsRead = e.IsRead,
            CreatedAt = new DateTimeOffset(e.CreatedAt).ToUnixTimeMilliseconds(),
            AvatarUrl = avatarUrl ?? string.Empty,
        };
    }

    /// <summary>构造单条消息 DTO（发送场景，按 FromUserId 单独查询发送者头像）。</summary>
    private async Task<ChatMessageDto> ToDtoAsync(ChatMessageEntity e)
    {
        var avatarUrl = await _db.Users
            .Where(u => u.UserId == e.FromUserId)
            .Select(u => u.AvatarUrl)
            .FirstOrDefaultAsync();

        return new ChatMessageDto
        {
            Id = e.Id,
            FromUserId = e.FromUserId,
            ToUserId = e.ToUserId,
            Content = e.Content,
            IsRead = e.IsRead,
            CreatedAt = new DateTimeOffset(e.CreatedAt).ToUnixTimeMilliseconds(),
            AvatarUrl = avatarUrl ?? string.Empty,
        };
    }
}
