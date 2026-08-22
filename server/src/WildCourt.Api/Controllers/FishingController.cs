using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 钓鱼事件上报（文档 02 §2.2）：客户端钓鱼成功/失败结算时
/// POST /api/v1/fishing/event 上报事件数据，服务端落库供管理端统计。
/// </summary>
[ApiController]
[Route("api/v1/fishing")]
public class FishingController : ControllerBase
{
    private readonly FishingService _fishingService;

    public FishingController(FishingService fishingService)
    {
        _fishingService = fishingService;
    }

    /// <summary>上报钓鱼结算事件（成功/失败）。</summary>
    [HttpPost("event")]
    public async Task<ApiResponse<object>> ReportEvent([FromBody] FishingEventRequest request)
    {
        if (string.IsNullOrEmpty(request.EventType))
        {
            throw new BusinessException(ApiCodes.BadRequest, "eventType 不能为空");
        }

        await _fishingService.RecordEventAsync(
            GetUserId(),
            request.EventType,
            request.BaitId ?? string.Empty,
            request.FishId,
            request.FishName,
            request.Reason,
            request.TimestampMs);

        return ApiResponse<object>.Ok(new { received = true });
    }

    private long GetUserId()
    {
        if (HttpContext.Items.TryGetValue("UserId", out var value) && value is long userId)
        {
            return userId;
        }
        throw new BusinessException(ApiCodes.Unauthorized, "未登录");
    }
}

/// <summary>
/// 钓鱼事件上报请求体（与客户端 FishingEventDto 字段一致，camelCase）。
/// </summary>
public class FishingEventRequest
{
    /// <summary>事件类型：success / fail。</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>本次消耗鱼饵ID。</summary>
    public string? BaitId { get; set; }

    /// <summary>成功时鱼种ID，失败为 null。</summary>
    public string? FishId { get; set; }

    /// <summary>成功时鱼名，失败为 null。</summary>
    public string? FishName { get; set; }

    /// <summary>失败原因/成功说明。</summary>
    public string? Reason { get; set; }

    /// <summary>客户端事件发生的 Unix 毫秒时间戳。</summary>
    public long TimestampMs { get; set; }
}
