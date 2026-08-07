using System;
using System.Collections.Generic;

namespace WildCourt.Net.Protocol
{
    // ---------- Yard（文档 03 庭院建造，YardController） ----------

    /// <summary>已摆放装饰项（文档 03 §3.8：等轴测网格 + 旋转）。</summary>
    [Serializable]
    public class YardItemDto
    {
        public string id;      // 装饰ID（D001 等）
        public int x;          // 网格 X（斜 45° 等轴测）
        public int y;          // 网格 Y
        public int rotation;   // 0/90/180/270
        public bool mirror;    // 镜像（水平翻转，与旋转叠加）
    }

    /// <summary>庭院解锁容量（Sheet16 YardUnlock，文档 03 §3.5.1）。</summary>
    [Serializable]
    public class YardUnlockConfigDto
    {
        public int level;
        public int farmlandCount;
        public int processorSlots;
        public int foodBowlCapacity;
        public int decorationLimit;
        public int showcaseSlots;
        public int mailboxCapacity;
        public string unlockCond;
    }

    /// <summary>一个方案槽的布局（1~3）。</summary>
    [Serializable]
    public class YardSlotDto
    {
        public int slotIndex;
        public List<YardItemDto> items;
    }

    /// <summary>庭院响应（GET /api/v1/yard、/visit/{userId}、PUT /layout）。</summary>
    [Serializable]
    public class YardResponse
    {
        public long userId;
        public int level;
        public int currentSlot;
        public List<YardSlotDto> slots;
        public YardUnlockConfigDto cap;
        public string updatedAt; // 服务端 DateTime → ISO 字符串
    }

    /// <summary>保存布局请求（PUT /api/v1/yard/layout）。</summary>
    [Serializable]
    public class SaveLayoutRequest
    {
        public int slotIndex;                       // 1~3
        public List<YardItemDto> items;             // 当前槽的装饰列表
    }
}
