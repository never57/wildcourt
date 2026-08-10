using Microsoft.AspNetCore.Mvc;
using WildCourt.Api.Models;
using WildCourt.Api.Services;

namespace WildCourt.Api.Controllers;

/// <summary>
/// 商店：货架、购买（阶段 B：装饰 + 庭贝；种子/材料/道具/鱼饵待补）（文档 02 §2.2、09）。
/// </summary>
[ApiController]
[Route("api/v1/shop")]
public class ShopController : ControllerBase
{
    private readonly ShopService _shopService;

    public ShopController(ShopService shopService)
    {
        _shopService = shopService;
    }

    /// <summary>货架列表</summary>
    [HttpGet("shelves")]
    public async Task<ApiResponse<List<ShelfItem>>> GetShelves()
    {
        var items = await _shopService.GetShelvesAsync();
        return ApiResponse<List<ShelfItem>>.Ok(items);
    }

    /// <summary>购买（双货币）</summary>
    [HttpPost("buy")]
    public async Task<ApiResponse<BuyResponse>> Buy([FromBody] BuyRequest request)
    {
        var result = await _shopService.BuyAsync(GetUserId(), request.ItemId, request.Count, request.Currency);
        return ApiResponse<BuyResponse>.Ok(result);
    }

    /// <summary>出售原始作物换庭贝（文档 09 §9.2：仅原始作物可售）。</summary>
    [HttpPost("sell")]
    public async Task<ApiResponse<BuyResponse>> Sell([FromBody] SellRequest request)
    {
        var result = await _shopService.SellAsync(GetUserId(), request.ItemId, request.Count);
        return ApiResponse<BuyResponse>.Ok(result);
    }

    /// <summary>鱼饵货架</summary>
    [HttpGet("bait-shelf")]
    public Task<ApiResponse<List<object>>> GetBaitShelf()
    {
        throw new NotImplementedException();
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
