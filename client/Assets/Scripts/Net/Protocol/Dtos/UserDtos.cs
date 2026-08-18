using System;

namespace WildCourt.Net.Protocol
{
    // ---------- User（文档 02 §2.2 UserController） ----------

    [Serializable]
    public class RegisterRequest
    {
        public string username;   // 账号（4~20 位字母数字下划线）
        public string password;   // 客户端 SHA256 哈希后传输
    }

    [Serializable]
    public class LoginRequest
    {
        public string username;
        public string password;   // 客户端 SHA256 哈希后传输
    }

    /// <summary>登录响应 data（服务端 LoginResponse）。</summary>
    [Serializable]
    public class LoginResponse
    {
        public string token;      // 会话 token，登录后注入 NetManager
        public long userId;
    }

    /// <summary>玩家信息（响应 data）。</summary>
    [Serializable]
    public class UserProfileResponse
    {
        public long userId;
        public string nickname;
        public string avatarUrl;  // 头像 URL（可为 null）
        public int provinceId;
        public int cityId;
        public int level;
        public int exp;           // 玩家经验（详情展示用，服务端已返回）
        public long shell;        // 庭贝
        public long coupon;       // 邮券（仅活动产出，文档 09 §9.1.1）
        public string token;      // 会话 token，登录后注入 NetManager
    }

    [Serializable]
    public class LevelResponse
    {
        public int level;
        public int exp;
    }

    /// <summary>修改昵称请求（PUT /api/v1/user/nickname）。</summary>
    [Serializable]
    public class UpdateNicknameRequest
    {
        public string nickname;
    }

    /// <summary>更新省份城市请求（PUT /api/v1/user/location）。</summary>
    [Serializable]
    public class UpdateLocationRequest
    {
        public int provinceId;
        public int cityId;
    }

    /// <summary>玩家省份城市信息（GET /api/v1/user/location，响应 data）。</summary>
    [Serializable]
    public class LocationResponse
    {
        public int provinceId;
        public int cityId;
    }

    /// <summary>搜索到的玩家卡片（GET /api/v1/user/search，响应 data 数组元素）。</summary>
    [Serializable]
    public class PlayerSearchDto
    {
        public long userId;
        public string nickname;
        public string avatarUrl;
        public string signature;
        public int level;
        public bool isFriend;
    }

    /// <summary>增加经验请求（联调测试接口 POST /api/v1/user/exp，alpha 阶段）。</summary>
    [Serializable]
    public class AddExpRequest
    {
        public int exp;
    }

    /// <summary>背包条目（GET /api/v1/user/inventory，阶段 C“已拥有”状态）。</summary>
    [Serializable]
    public class InventoryItem
    {
        public string itemId;
        public int count;
    }

    /// <summary>背包响应（信封 data）。</summary>
    [Serializable]
    public class InventoryResponse
    {
        public InventoryItem[] items;
    }
}
