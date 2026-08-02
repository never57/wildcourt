using WlidCourt.Api.Models;

namespace WlidCourt.Api.Services;

/// <summary>
/// 庭院业务：布局 CRUD、等级容量校验（农田 6→12、槽位 2→5、食物盆 10→20、装饰 50→200、展示柜 3→9、信箱 20→50）、互访快照（文档 02 §2.3）。
/// </summary>
public class YardService
{
    /// <summary>我的庭院</summary>
    public Task<Yard> GetAsync(long userId)
    {
        throw new NotImplementedException();
    }

    /// <summary>保存布局（校验建筑/装饰上限）</summary>
    public Task SaveLayoutAsync(long userId, string layoutJson, int slotIndex)
    {
        throw new NotImplementedException();
    }
}
