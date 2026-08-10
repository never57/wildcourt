using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 庭院：布局、容量、互访（文档 02 §2.2 / 文档 03）。
/// </summary>
[ApiController]
[Route("api/v1/yard")]
public class YardController : ControllerBase
{
    private readonly YardService _yardService;
    private readonly CropService _cropService;

    public YardController(YardService yardService, CropService cropService)
    {
        _yardService = yardService;
        _cropService = cropService;
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

    /// <summary>种植作物（客户端 Lua 识别作物并计算结束时间后上报，服务端校验并落库）。</summary>
    [HttpPost("plant")]
    public async Task<ApiResponse<FarmPlotDto>> Plant([FromBody] PlantCropRequest request)
    {
        var plot = await _cropService.PlantAsync(GetUserId(), request.CellId, request.CropId, request.ReadyAt);
        return ApiResponse<FarmPlotDto>.Ok(plot);
    }

    /// <summary>我的农田种植状态（客户端恢复本地倒计时用）。</summary>
    [HttpGet("plots")]
    public async Task<ApiResponse<FarmPlotsResponse>> GetPlots()
    {
        var plots = await _cropService.GetPlotsAsync(GetUserId());
        return ApiResponse<FarmPlotsResponse>.Ok(plots);
    }

    private long GetUserId()
    {
        if (HttpContext.Items.TryGetValue("UserId", out var value) && value is long userId)
            return userId;
        throw new BusinessException(ApiCodes.Unauthorized, "未登录");
    }
}
