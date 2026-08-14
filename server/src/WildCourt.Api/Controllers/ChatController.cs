using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WildCourt.Api.Hubs;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>好友私聊：发送（入库 + 实时推送）、历史记录。</summary>
[ApiController]
[Route("api/v1/chat")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly IHubContext<ChatHub> _hub;

    public ChatController(ChatService chatService, IHubContext<ChatHub> hub)
    {
        _chatService = chatService;
        _hub = hub;
    }

    /// <summary>发送私聊消息：入库后经 SignalR 推送给接收者（在线则实时，离线则下次拉历史）。</summary>
    [HttpPost("send")]
    public async Task<ApiResponse<ChatMessageDto>> Send([FromBody] SendChatRequest request)
    {
        var result = await _chatService.SendAsync(GetUserId(), request.ToUserId, request.Content);

        // 推送给接收者（在线实时收到）；发送者连接无需回推（客户端本地已上屏）
        await _hub.Clients.User(request.ToUserId.ToString())
            .SendAsync("ReceivePrivateMessage", result);

        return ApiResponse<ChatMessageDto>.Ok(result);
    }

    /// <summary>与某好友的历史消息（可选 beforeId 分页往前翻）。</summary>
    [HttpGet("history/{friendUserId:long}")]
    public async Task<ApiResponse<ChatHistoryResponse>> History(long friendUserId, [FromQuery] long? beforeId, [FromQuery] int limit = 50)
    {
        var result = await _chatService.HistoryAsync(GetUserId(), friendUserId, beforeId, limit);
        return ApiResponse<ChatHistoryResponse>.Ok(result);
    }

    private long GetUserId()
    {
        if (HttpContext.Items.TryGetValue("UserId", out var value) && value is long userId)
            return userId;
        throw new BusinessException(ApiCodes.Unauthorized, "未登录");
    }
}
