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

    // ---------- Pet（PetController） ----------

    /// <summary>宠物信息（响应 data）。status: 在家 / 派遣中 / 休息中。</summary>
    [Serializable]
    public class PetInfoResponse
    {
        public long petId;
        public string type;
        public int level;
        public int exp;
        public string status;
    }

    /// <summary>派遣请求（文档 07 §7.6 十步校验；cityId 为 0 表示不限城市）。</summary>
    [Serializable]
    public class DispatchRequest
    {
        public int provinceId;
        public int cityId;        // 0 = 不限
        public string nonfictionId; // 随行非遗
        public string letterContent; // ≤50 字
    }

    [Serializable]
    public class DispatchResultResponse
    {
        public bool success;
        public string message;
        public long targetUserId; // 匹配到的目标庭院
    }

    [Serializable]
    public class RecallRequest
    {
        public long petId;
    }
}
