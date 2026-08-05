using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WlidCourt.Api.Models;
using WlidCourt.Data.DbContext;
using WlidCourt.Data.Entities;

namespace WlidCourt.Api.Services;

/// <summary>
/// 庭院业务（文档 03）：布局 CRUD、3 个方案槽、等级容量校验（Sheet16 YardUnlock）、互访快照。
/// layout_json 结构：{ "currentSlot": 1, "slots": { "1": [YardItemDto...], "2": [], "3": [] } }。
/// </summary>
public class YardService
{
    private const string YardUnlockFile = "Config_YardUnlock.json";
    private const string DecorationsFile = "Config_Decorations.json";
    public const int LayoutSlotCount = 3;

    /// <summary>围墙网格尺寸（文档 03 §3.8 边界限制；与客户端 YardManager 常量一致，坐标 0~19）。</summary>
    public const int YardWidth = 20;
    public const int YardHeight = 20;

    private readonly WlidCourtDbContext _db;
    private readonly ConfigService _config;

    public YardService(WlidCourtDbContext db, ConfigService config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>我的庭院；不存在则创建默认庭院（注册时也应已建，兜底）。</summary>
    public async Task<YardResponse> GetAsync(long userId)
    {
        var yard = await GetOrCreateAsync(userId);
        return await ToResponseAsync(yard);
    }

    /// <summary>保存指定方案槽的布局（校验槽位/容量/装饰存在/重叠）。</summary>
    public async Task<YardResponse> SaveLayoutAsync(long userId, int slotIndex, List<YardItemDto> items)
    {
        if (slotIndex < 1 || slotIndex > LayoutSlotCount)
        {
            throw new BusinessException(ApiCodes.BadRequest, $"布局槽须为 1~{LayoutSlotCount}");
        }

        var yard = await GetOrCreateAsync(userId);
        var user = await _db.Users.FindAsync(userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "用户不存在");
        var cap = GetUnlockConfig(user.Level);

        // 校验：装饰数量上限（文档 03 §3.5.1 decorationLimit）
        if (items.Count > cap.DecorationLimit)
        {
            throw new BusinessException(ApiCodes.BadRequest, $"装饰数量超出上限（{cap.DecorationLimit}）");
        }

        // 校验：装饰 ID 存在（Config_Decorations.json）
        var decorations = _config.GetRows<DecorationConfig>(DecorationsFile);
        var byId = decorations.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!byId.ContainsKey(item.Id))
            {
                throw new BusinessException(ApiCodes.BadRequest, $"装饰不存在: {item.Id}");
            }
        }

        // 校验：装饰必须已拥有，且同 ID 摆放数 ≤ min(拥有数, 配置上限 maxCount)
        var ownedRows = await _db.Inventories
            .Where(i => i.UserId == userId && i.Count > 0)
            .Select(i => new { i.ItemId, i.Count })
            .ToListAsync();
        var ownedMap = ownedRows.ToDictionary(r => r.ItemId, r => r.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!ownedMap.ContainsKey(item.Id))
            {
                throw new BusinessException(ApiCodes.BadRequest, $"未拥有装饰: {item.Id}");
            }
        }

        var placedCount = items
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        foreach (var kv in placedCount)
        {
            var deco = byId[kv.Key];
            // 拥有 ≥1 即可摆到配置上限 maxCount（不受实际背包数量限制）
            int limit = deco.MaxCount;
            if (kv.Value > limit)
            {
                throw new BusinessException(ApiCodes.BadRequest, $"装饰 {kv.Key} 摆放数量超出上限（{limit}）");
            }
        }

        // 校验：不可超出围墙范围（文档 03 §3.8 边界限制，旋转后尺寸参与计算）
        foreach (var item in items)
        {
            var (x, y, w, h) = GetRect(item, byId);
            if (x < 0 || y < 0 || x + w > YardWidth || y + h > YardHeight)
            {
                throw new BusinessException(ApiCodes.BadRequest, "装饰超出围墙范围");
            }
        }

        // 校验：同一网格不可重叠（文档 03 §3.8），尺寸按旋转交换宽高
        ValidateNoOverlap(items, byId);

        // 存储：更新对应槽，保留其他槽
        var layout = DeserializeLayout(yard.LayoutJson);
        layout.CurrentSlot = slotIndex;
        layout.Slots[slotIndex] = items;
        yard.LayoutJson = JsonSerializer.Serialize(layout);
        yard.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await ToResponseAsync(yard);
    }

    /// <summary>互访庭院快照（文档 03 §3.11）。</summary>
    public async Task<YardResponse> VisitAsync(long userId)
    {
        return await GetAsync(userId);
    }

    /// <summary>创建默认庭院（注册时调用；空布局 3 槽）。</summary>
    public async Task<YardEntity> CreateDefaultAsync(long userId)
    {
        var yard = new YardEntity
        {
            UserId = userId,
            LayoutJson = JsonSerializer.Serialize(new YardLayout()),
            Level = 1,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Yards.Add(yard);
        await _db.SaveChangesAsync();
        return yard;
    }

    // ---- 内部 ----

    private async Task<YardEntity> GetOrCreateAsync(long userId)
    {
        var yard = await _db.Yards.FindAsync(userId);
        if (yard == null)
        {
            yard = await CreateDefaultAsync(userId);
        }
        return yard;
    }

    private async Task<YardResponse> ToResponseAsync(YardEntity yard)
    {
        var user = await _db.Users.FindAsync(yard.UserId);
        var level = user?.Level ?? 1;
        var layout = DeserializeLayout(yard.LayoutJson);
        return new YardResponse
        {
            UserId = yard.UserId,
            Level = level,
            CurrentSlot = layout.CurrentSlot,
            Slots = layout.Slots
                .OrderBy(s => s.Key)
                .Select(s => new YardSlotDto { SlotIndex = s.Key, Items = s.Value })
                .ToList(),
            Cap = GetUnlockConfig(level),
            UpdatedAt = yard.UpdatedAt,
        };
    }

    /// <summary>按玩家等级取庭院容量（Config_YardUnlock.json；查不到时按满级兜底）。</summary>
    private YardUnlockConfig GetUnlockConfig(int level)
    {
        var rows = _config.GetRows<YardUnlockConfig>(YardUnlockFile);
        return rows.FirstOrDefault(r => r.Level == level)
            ?? rows.OrderByDescending(r => r.Level).FirstOrDefault()
            ?? new YardUnlockConfig { Level = level };
    }

    private static void ValidateNoOverlap(List<YardItemDto> items, Dictionary<string, DecorationConfig> byId)
    {
        var rects = items.Select(item => (Item: item, Rect: GetRect(item, byId))).ToList();

        for (int i = 0; i < rects.Count; i++)
        {
            for (int j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i];
                var b = rects[j];
                bool overlap = a.Item.X < b.Rect.X + b.Rect.W && a.Item.X + a.Rect.W > b.Rect.X &&
                               a.Item.Y < b.Rect.Y + b.Rect.H && a.Item.Y + a.Rect.H > b.Rect.Y;
                if (overlap)
                {
                    throw new BusinessException(ApiCodes.BadRequest, $"装饰重叠: {a.Item.Id} 与 {b.Item.Id}");
                }
            }
        }
    }

    /// <summary>计算装饰占格矩形（旋转 90/270 时宽高交换，文档 03 §3.8）。</summary>
    private static (int X, int Y, int W, int H) GetRect(YardItemDto item, Dictionary<string, DecorationConfig> byId)
    {
        var d = byId[item.Id];
        bool swap = d.SizeX != d.SizeY && (item.Rotation == 90 || item.Rotation == 270);
        int w = swap ? d.SizeY : d.SizeX;
        int h = swap ? d.SizeX : d.SizeY;
        return (item.X, item.Y, w, h);
    }

    internal static YardLayout DeserializeLayout(string? layoutJson)
    {
        if (string.IsNullOrEmpty(layoutJson))
        {
            return new YardLayout();
        }
        try
        {
            var layout = JsonSerializer.Deserialize<YardLayout>(layoutJson, JsonOpts);
            layout ??= new YardLayout();
            for (int i = 1; i <= LayoutSlotCount; i++)
            {
                layout.Slots.TryAdd(i, new List<YardItemDto>());
            }
            return layout;
        }
        catch (JsonException)
        {
            return new YardLayout();
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>layout_json 内部结构（slots 的 key 序列化为 "1"/"2"/"3"）。</summary>
    public sealed class YardLayout
    {
        public int CurrentSlot { get; set; } = 1;

        public Dictionary<int, List<YardItemDto>> Slots { get; set; } = new()
        {
            [1] = new List<YardItemDto>(),
            [2] = new List<YardItemDto>(),
            [3] = new List<YardItemDto>(),
        };
    }
}
