using WlidCourt.Api.Models;

namespace WlidCourt.Api.Services;

/// <summary>
/// 宠物业务：属性、状态机（在家→派遣中→休息中→在家）、经验（文档 02 §2.3）。
/// </summary>
public class PetService
{
    /// <summary>宠物信息</summary>
    public Task<Pet> GetAsync(long petId)
    {
        throw new NotImplementedException();
    }

    /// <summary>派遣（10 步校验）</summary>
    public Task<object> DispatchAsync(
        long petId,
        int provinceId,
        int? cityId,
        string nonfictionId,
        string letterContent)
    {
        throw new NotImplementedException();
    }

    /// <summary>派遣归来：状态+经验+徽章</summary>
    public Task SettleReturnAsync(long petId)
    {
        throw new NotImplementedException();
    }
}
