using System;

namespace WildCourt.Net.Protocol
{
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
