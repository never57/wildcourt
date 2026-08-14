using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 好友：申请、接受、偷菜、喂食、互访庭院（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/friend")]
public class FriendController : ControllerBase
{
    private readonly FriendService _friendService;

    public FriendController(FriendService friendService)
    {
        _friendService = friendService;
    }

    /// <summary>好友申请</summary>
    [HttpPost("apply")]
    public async Task<ApiResponse<object>> Apply([FromBody] FriendRequest request)
    {
        await _friendService.ApplyAsync(GetUserId(), request.UserId);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>接受申请</summary>
    [HttpPost("accept")]
    public async Task<ApiResponse<object>> Accept([FromBody] FriendRequest request)
    {
        await _friendService.AcceptAsync(GetUserId(), request.UserId);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>拒绝申请</summary>
    [HttpPost("reject")]
    public async Task<ApiResponse<object>> Reject([FromBody] FriendRequest request)
    {
        await _friendService.RejectAsync(GetUserId(), request.UserId);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>好友列表</summary>
    [HttpGet("list")]
    public async Task<ApiResponse<FriendListResponse>> List()
    {
        var result = await _friendService.ListAsync(GetUserId());
        return ApiResponse<FriendListResponse>.Ok(result);
    }

    /// <summary>待处理的好友申请列表</summary>
    [HttpGet("requests")]
    public async Task<ApiResponse<FriendRequestListResponse>> Requests()
    {
        var result = await _friendService.PendingRequestsAsync(GetUserId());
        return ApiResponse<FriendRequestListResponse>.Ok(result);
    }

    /// <summary>删除好友</summary>
    [HttpDelete("{userId:long}")]
    public async Task<ApiResponse<object>> Delete(long userId)
    {
        await _friendService.DeleteAsync(GetUserId(), userId);
        return ApiResponse<object>.Ok(new object());
    }

    /// <summary>偷菜（30% 产量向下取整、每好友每作物每天 1 次、每日 20 次，文档 08 §8.3）。</summary>
    [HttpPost("steal")]
    public async Task<ApiResponse<StealResultDto>> Steal([FromBody] StealCropRequest request)
    {
        var result = await _friendService.StealAsync(GetUserId(), request.TargetUserId, request.CellId);
        return ApiResponse<StealResultDto>.Ok(result);
    }

    /// <summary>喂食（3 次/天）</summary>
    [HttpPost("feed")]
    public Task<ApiResponse<object>> Feed([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>互访庭院</summary>
    [HttpGet("yard/{userId}")]
    public Task<ApiResponse<Yard>> GetYard(long userId)
    {
        throw new NotImplementedException();
    }

    private long GetUserId()
    {
        if (HttpContext.Items.TryGetValue("UserId", out var value) && value is long userId)
            return userId;
        throw new BusinessException(ApiCodes.Unauthorized, "未登录");
    }
}
