using Microsoft.AspNetCore.Mvc;
using WlidCourt.Api.Models;
using WlidCourt.Api.Services;

namespace WlidCourt.Api.Controllers;

/// <summary>
/// 庭院：布局、容量、互访（文档 02 §2.2 / 文档 03）。
/// </summary>
[ApiController]
[Route("api/v1/yard")]
public class YardController : ControllerBase
{
    private readonly YardService _yardService;

    public YardController(YardService yardService)
    {
        _yardService = yardService;
    }

    /// <summary>我的庭院（含 3 个布局槽 + 容量）。</summary>
    [HttpGet]
    public async Task<ApiResponse<YardResponse>> GetYard()
    {
        var yard = await _yardService.GetAsync(GetUserId());
        return ApiResponse<YardResponse>.Ok(yard);
    }

    /// <summary>保存布局到指定方案槽（服务端校验容量/重叠）。</summary>
    [HttpPut("layout")]
    public async Task<ApiResponse<YardResponse>> SaveLayout([FromBody] SaveLayoutRequest request)
    {
        var yard = await _yardService.SaveLayoutAsync(GetUserId(), request.SlotIndex, request.Items);
        return ApiResponse<YardResponse>.Ok(yard);
    }

    /// <summary>互访庭院（文档 03 §3.11）。</summary>
    [HttpGet("visit/{userId:long}")]
    public async Task<ApiResponse<YardResponse>> Visit(long userId)
    {
        var yard = await _yardService.VisitAsync(userId);
        return ApiResponse<YardResponse>.Ok(yard);
    }

    private long GetUserId()
    {
        if (HttpContext.Items.TryGetValue("UserId", out var value) && value is long userId)
            return userId;
        throw new BusinessException(ApiCodes.Unauthorized, "未登录");
    }
}
