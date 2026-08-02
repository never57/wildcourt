using Microsoft.AspNetCore.Mvc;
using WlidCourt.Api.Models;

namespace WlidCourt.Api.Controllers;

/// <summary>
/// 商店：货架、购买（庭贝/邮券双货币）、鱼饵货架（文档 02 §2.2）。
/// </summary>
[ApiController]
[Route("api/v1/shop")]
public class ShopController : ControllerBase
{
    /// <summary>货架列表</summary>
    [HttpGet("shelves")]
    public Task<ApiResponse<List<object>>> GetShelves()
    {
        throw new NotImplementedException();
    }

    /// <summary>购买（双货币）</summary>
    [HttpPost("buy")]
    public Task<ApiResponse<object>> Buy([FromBody] object request)
    {
        throw new NotImplementedException();
    }

    /// <summary>鱼饵货架</summary>
    [HttpGet("bait-shelf")]
    public Task<ApiResponse<List<object>>> GetBaitShelf()
    {
        throw new NotImplementedException();
    }
}
