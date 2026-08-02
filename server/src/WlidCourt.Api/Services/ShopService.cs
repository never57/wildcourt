namespace WlidCourt.Api.Services;

/// <summary>
/// 商店业务：双货币货架、购买校验、库存（文档 02 §2.3）。
/// </summary>
public class ShopService
{
    /// <summary>货架列表</summary>
    public Task<List<object>> GetShelvesAsync()
    {
        throw new NotImplementedException();
    }

    /// <summary>购买（种子/材料/道具/鱼饵）</summary>
    public Task BuyAsync(long userId, string itemId, int count, string currency)
    {
        throw new NotImplementedException();
    }
}
