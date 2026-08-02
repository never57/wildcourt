using WlidCourt.Api.Models;

namespace WlidCourt.Api.Services;

/// <summary>
/// 用户业务：注册/登录/身份；会话签发（文档 02 §2.3）。
/// </summary>
public class UserService
{
    /// <summary>注册（选省/市）</summary>
    public Task<User> RegisterAsync(string nickname, int provinceId, int cityId)
    {
        throw new NotImplementedException();
    }

    /// <summary>登录，签发会话（写 user_session:{token}）</summary>
    public Task<string> LoginAsync(long userId)
    {
        throw new NotImplementedException();
    }

    /// <summary>玩家信息</summary>
    public Task<User> GetProfileAsync(long userId)
    {
        throw new NotImplementedException();
    }
}
