using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>玩家举报：A 举报 B。</summary>
[ApiController]
[Route("api/v1/report")]
public class ReportController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportController(ReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>发起举报（reportedUserId + reason）。</summary>
    [HttpPost]
    public async Task<ApiResponse<object>> Report([FromBody] CreateReportRequest request)
    {
        await _reportService.ReportAsync(GetUserId(), request.ReportedUserId, request.Reason);
        return ApiResponse<object>.Ok(new object());
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
