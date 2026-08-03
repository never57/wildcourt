using System;

namespace WildCourt.Net.Protocol
{
    // ---------- User（文档 02 §2.2 UserController） ----------

    [Serializable]
    public class RegisterRequest
    {
        public string nickname;   // 昵称
        public int provinceId;    // 所选省份（1~34）
        public int cityId;        // 所选城市
    }

    [Serializable]
    public class LoginRequest
    {
        public long userId;       // 简化登录：正式版以账号/设备 token 换取会话
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
