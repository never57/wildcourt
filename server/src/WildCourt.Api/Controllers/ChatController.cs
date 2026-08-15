using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>好友私聊：发送（入库 + 实时推送）、历史记录。</summary>
[ApiController]
[Route("api/v1/chat")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;

    public ChatController(ChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>发送私聊消息：入库后由 ChatService 经 WebSocket 推送给接收者（在线则实时，离线则下次拉历史）。</summary>
    [HttpPost("send")]
    public async Task<ApiResponse<ChatMessageDto>> Send([FromBody] SendChatRequest request)
    {
        var result = await _chatService.SendAsync(GetUserId(), request.ToUserId, request.Content);
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
