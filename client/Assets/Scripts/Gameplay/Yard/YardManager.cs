using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WildCourt.Core;
using WildCourt.Lua;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.Utils;

#if USE_XLUA
using XLua;
#endif

namespace WildCourt.Gameplay.Yard
{
    /// <summary>
    /// 庭院建造（文档 03）：等轴测 45° 摆放、编辑模式（选择/放置/移动/旋转/删除）、
    /// 3 个布局方案槽、容量校验（Sheet16 YardUnlock）、互访庭院渲染（UI 层负责）。
    /// 服务端为权威：PUT /api/v1/yard/layout 校验容量与重叠；本地先做同样校验做即时反馈。
    /// UI 挂钩：订阅 <see cref="YardLoaded"/>/<see cref="LayoutChanged"/>/<see cref="Error"/>，
    /// 渲染由场景脚本完成（文档 03 §3.9/§3.10）。
    /// </summary>
    public class YardManager : GameplayManager<YardManager>
    {
        public const int LayoutSlotCount = 3;

        /// <summary>围墙网格尺寸（文档 03 §3.8 边界限制；与服务端 YardService 常量一致，坐标 0~19）。</summary>
        public const int YardWidth = 20;
        public const int YardHeight = 20;

        /// <summary>
        /// 可放置边界（逻辑坐标，含边界）。默认 20×20 全庭院；
        /// 渲染层初始化后可按瓦片地图实际范围收紧（瓦片没铺满时，防止装饰悬空在无地面区域）。
        /// </summary>
        public int PlaceXMin = 0;
        public int PlaceYMin = 0;
        public int PlaceXMax = YardWidth - 1;
        public int PlaceYMax = YardHeight - 1;

        /// <summary>编辑模式状态（文档 03 §3.9：放置/移动/旋转/删除）。</summary>
        public enum EditState
        {
            /// <summary>非编辑状态。</summary>
            Idle,

            /// <summary>已选装饰，等待放置（光标预览，绿=可放/红=不可放）。</summary>
            Placing,
        }

        // ---- 状态（UI 读取）----

        public EditState State { get; private set; } = EditState.Idle;

        /// <summary>当前方案槽（1~3）。</summary>
        public int CurrentSlot { get; private set; } = 1;

        /// <summary>当前槽已摆放装饰（文档 03 §3.9）。</summary>
        public List<YardItemDto> Items { get; private set; } = new();

        /// <summary>正在放置的装饰（Placing 状态时非空）。</summary>
        public DecorationConfig SelectedDecoration { get; private set; }

        public int PlayerLevel { get; private set; } = 1;

        /// <summary>当前等级容量（文档 03 §3.5.1）。</summary>
        public YardUnlockConfigDto Cap { get; private set; } = new();

        // ---- 事件（UI 订阅）----

        /// <summary>庭院从服务端加载完成。</summary>
        public event Action YardLoaded;

        /// <summary>当前槽布局变化（放置/移动/旋转/删除后）。</summary>
        public event Action LayoutChanged;

        /// <summary>错误（网络/校验失败）。</summary>
        public event Action<string> Error;

        private DecorationConfig[] _decorations = Array.Empty<DecorationConfig>();

        /// <summary>热更 Lua 校验层是否已就绪（provider 注入成功）。桩模式 / 模块缺失时为 false，校验回退本地 C#。</summary>
        private static bool _luaReady;

        /// <summary>本地三槽缓存（未保存的改动保留在切换槽期间）。</summary>
        private readonly Dictionary<int, List<YardItemDto>> _localSlots = new();

        /// <summary>背包拥有量：itemId → count。</summary>
        private readonly Dictionary<string, int> _owned = new();

        private void Start()
        {
            _decorations = ConfigMgr.Instance.GetTable<DecorationConfig>("Decorations");
            if (!ConfigMgr.Instance.IsLoaded)
            {
                Debug.LogWarning("[YardManager] ConfigMgr 未加载配置，装饰列表为空（请先 ConfigMgr.LoadAll()）");
            }

            // 注入 Lua 侧数据源：配置查询委托回 C#（热更逻辑与数据解耦）。
            InjectLuaProvider();

            // 热更重载后 Lua 模块被重新 require，provider 会重置回默认值，
            // 订阅 ConfigReloadedEvent 在重载完成后重新注入，闭合热更链路。
            EventBus.Subscribe<ConfigReloadedEvent>(OnConfigReloaded);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ConfigReloadedEvent>(OnConfigReloaded);
        }

        private void OnConfigReloaded(ConfigReloadedEvent evt)
        {
            // 配置表已重载，刷新本地装饰配置引用，再重新注入 Lua provider。
            _decorations = ConfigMgr.Instance.GetTable<DecorationConfig>("Decorations");
            _luaReady = false;
            InjectLuaProvider();
        }

        /// <summary>
        /// 把配置查询注入 <c>Logic.Yard.YardManager</c>（热更 Lua 的 provider）。
        /// 桩模式（未启用 USE_XLUA）下 CallFunc 直接返回默认值，校验退化为放行，
        /// 服务端仍会做权威校验兜底。
        /// </summary>
        private void InjectLuaProvider()
        {
#if USE_XLUA
            try
            {
                var env = LuaEnvHolder.Env;
                if (env == null) return;
                // provider.getDecoration：Lua 侧需要 { sizeX, sizeY, maxCount, rotatable }
                Func<string, LuaTable> getDecoration = id =>
                {
                    var d = GetDecoration(id);
                    if (d == null) return null;
                    var t = env.NewTable();
                    t.Set("sizeX", d.sizeX);
                    t.Set("sizeY", d.sizeY);
                    t.Set("maxCount", d.maxCount);
                    t.Set("rotatable", d.rotatable);
                    return t;
                };
                var provider = env.NewTable();
                provider.Set("getDecoration", getDecoration);
                LuaManager.Instance.CallFunc<bool>(
                    "Logic.Yard.YardManager", "SetDataProvider", provider);
                _luaReady = true;
                Debug.Log("[YardManager] Lua 校验层 provider 注入成功");
            }
            catch (Exception e)
            {
                _luaReady = false;
                Debug.LogWarning($"[YardManager] Lua provider 注入失败（回退 C# 逻辑）: {e.Message}");
            }
#endif
        }

        /// <summary>加载我的庭院（GET /api/v1/yard）。</summary>
        public void LoadYard()
        {
            NetManager.Instance.Get<YardResponse>(ApiPaths.YardGet,
                resp => ApplyYard(resp.data),
                (code, msg) => Error?.Invoke(msg));
        }

        // ---- 编辑模式（文档 03 §3.9）----

        public void EnterEditMode()
        {
            State = EditState.Idle;
        }

        public void ExitEditMode()
        {
            State = EditState.Idle;
            SelectedDecoration = null;
        }

        /// <summary>选择装饰开始放置（列表图标 → 光标预览）。</summary>
        public void BeginPlace(string decorationId)
        {
            SelectedDecoration = _decorations.FirstOrDefault(d => d.id == decorationId);
            if (SelectedDecoration == null)
            {
                Error?.Invoke($"装饰不存在: {decorationId}");
                return;
            }
            State = EditState.Placing;
        }

        /// <summary>尝试放置到网格 (x, y)；绿/红反馈由 UI 调 <see cref="CanPlaceAt"/>。</summary>
        public bool TryPlace(int x, int y)
        {
            if (State != EditState.Placing || SelectedDecoration == null) return false;
            var item = new YardItemDto { id = SelectedDecoration.id, x = x, y = y, rotation = 0 };
            if (!CanPlaceAt(item)) return false;
            Items.Add(item);
            State = EditState.Idle;
            SelectedDecoration = null;
            LayoutChanged?.Invoke();
            return true;
        }

        /// <summary>放置预览：目标网格 (x,y) 是否可放（容量 + 重叠）。</summary>
        public bool CanPlaceAt(int x, int y)
        {
            if (SelectedDecoration == null) return false;
            return CanPlaceAt(new YardItemDto { id = SelectedDecoration.id, x = x, y = y, rotation = 0 });
        }

        /// <summary>移动已放置装饰（文档 03 §3.9：点击→移动→拖到新位置）。</summary>
        public void MoveItem(int index, int x, int y)
        {
            if (index < 0 || index >= Items.Count) return;
            var item = Items[index];
            Items.RemoveAt(index);
            item.x = x;
            item.y = y;
            if (CanPlaceAt(item))
            {
                Items.Insert(Math.Min(index, Items.Count), item);
                LayoutChanged?.Invoke();
            }
            else
            {
                Items.Insert(Math.Min(index, Items.Count), item); // 目标位不可放 → 留在原位
            }
        }

        /// <summary>移动目标位是否合法（阶段 C：拖放预览用，重叠判定排除自身）。</summary>
        public bool CanMoveTo(int index, int x, int y)
        {
            if (index < 0 || index >= Items.Count) return false;
            var item = Items[index];

            // 优先走热更 Lua 校验（排除自身后交 Lua 判重叠）。
            if (_luaReady)
            {
                var args = new List<object>
                {
                    item.id, x, y, item.rotation,
                    PlaceXMin, PlaceYMin, PlaceXMax, PlaceYMax,
                };
                args.AddRange(FlatItems(Items, item));
                return LuaManager.Instance.CallFuncArray<bool>(
                    "Logic.Yard.YardManager", "CanMoveToById", args.ToArray());
            }

            // ---- 回退：本地 C# 校验 ----
            var d = GetDecoration(item.id);
            if (d == null) return false;
            var (w, h) = GetSize(d, item.rotation);
            if (!IsInPlaceBounds(x, y, w, h)) return false;

            for (int i = 0; i < Items.Count; i++)
            {
                if (i == index) continue;
                var other = Items[i];
                var od = GetDecoration(other.id);
                if (od == null) continue;
                var (ow, oh) = GetSize(od, other.rotation);
                bool overlap = x < other.x + ow && x + w > other.x &&
                               y < other.y + oh && y + h > other.y;
                if (overlap) return false;
            }
            return true;
        }

        /// <summary>旋转已放置装饰（顺时针 90°，文档 03 §3.9）。</summary>
        public void RotateItem(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            var item = Items[index];
            if (!GetDecoration(item.id)?.rotatable ?? false)
            {
                Error?.Invoke($"{item.id} 不支持旋转");
                return;
            }
            item.rotation = (item.rotation + 90) % 360;
            LayoutChanged?.Invoke();
        }

        /// <summary>镜像切换（水平翻转，与旋转叠加）。</summary>
        public void MirrorItem(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            Items[index].mirror = !Items[index].mirror;
            LayoutChanged?.Invoke();
        }

        /// <summary>删除已放置装饰（庭贝不返还，文档 03 §3.9）。</summary>
        public void RemoveItem(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            Items.RemoveAt(index);
            LayoutChanged?.Invoke();
        }

        /// <summary>切换方案槽（本地缓存；未保存改动保留在槽内）。</summary>
        public void SwitchSlot(int slot)
        {
            if (slot < 1 || slot > LayoutSlotCount || slot == CurrentSlot) return;
            _localSlots[CurrentSlot] = new List<YardItemDto>(Items);
            CurrentSlot = slot;
            Items = _localSlots.TryGetValue(slot, out var list) ? list : new List<YardItemDto>();
            LayoutChanged?.Invoke();
        }

        /// <summary>保存当前槽布局到服务端（PUT /api/v1/yard/layout，服务端校验）。</summary>
        public void SaveLayout()
        {
            var request = new SaveLayoutRequest { slotIndex = CurrentSlot, items = Items };
            NetManager.Instance.PutJson<YardResponse>(ApiPaths.YardLayout, JsonUtil.ToJson(request),
                resp =>
                {
                    ApplyYard(resp.data);
                    Debug.Log($"[YardManager] 布局已保存（槽 {CurrentSlot}）");
                },
                (code, msg) => Error?.Invoke(msg));
        }

        /// <summary>同步背包拥有量（UI 加载背包后调用）。</summary>
        public void SetOwned(Dictionary<string, int> owned)
        {
            _owned.Clear();
            if (owned != null)
            {
                foreach (var kv in owned) _owned[kv.Key] = kv.Value;
                // Dictionary 遍历无法完全避免分配；此处为低频回调，可接受。
            }
        }

        /// <summary>读取装饰配置（UI 列表用）。</summary>
        public DecorationConfig[] GetDecorations() => _decorations;

        public DecorationConfig GetDecoration(string id) => _decorations.FirstOrDefault(d => d.id == id);

        // ---- 内部 ----

        private void ApplyYard(YardResponse yard)
        {
            PlayerLevel = yard.level;
            Cap = yard.cap;
            CurrentSlot = yard.currentSlot;
            _localSlots.Clear();
            foreach (var slot in yard.slots)
            {
                _localSlots[slot.slotIndex] = slot.items ?? new List<YardItemDto>();
            }
            Items = _localSlots.TryGetValue(CurrentSlot, out var list) ? list : new List<YardItemDto>();
            YardLoaded?.Invoke();
            LayoutChanged?.Invoke();
        }

        /// <summary>矩形 (x,y)-(x+w-1, y+h-1) 是否完整落在可放置边界内。</summary>
        private bool IsInPlaceBounds(int x, int y, int w, int h)
        {
            return x >= PlaceXMin && y >= PlaceYMin &&
                   x + w - 1 <= PlaceXMax && y + h - 1 <= PlaceYMax;
        }

        private bool CanPlaceAt(YardItemDto item)
        {
            // 优先走热更 Lua 校验（规则可热更）；桩模式 / 模块缺失时回退本地 C# 校验。
            if (_luaReady)
            {
                var args = new List<object>
                {
                    item.id, item.x, item.y, item.rotation,
                    Items.Count, Cap.decorationLimit,
                    _owned.TryGetValue(item.id, out var owned) ? owned : 0,
                    PlacedCount(item.id),
                    PlaceXMin, PlaceYMin, PlaceXMax, PlaceYMax,
                };
                args.AddRange(FlatItems(Items, item));
                return LuaManager.Instance.CallFuncArray<bool>(
                    "Logic.Yard.YardManager", "CanPlaceAt", args.ToArray());
            }

            // ---- 回退：本地 C# 校验（与 Lua 规则一致）----
            if (Items.Count >= Cap.decorationLimit) return false;
            var d = GetDecoration(item.id);
            if (d == null) return false;
            int ownedFallback = _owned.TryGetValue(item.id, out var c) ? c : 0;
            int limit = Math.Min(ownedFallback, d.maxCount);
            if (PlacedCount(item.id) >= limit) return false;
            var (w, h) = GetSize(d, item.rotation);
            if (!IsInPlaceBounds(item.x, item.y, w, h)) return false;
            for (int i = 0; i < Items.Count; i++)
            {
                var other = Items[i];
                if (ReferenceEquals(other, item)) continue;
                var od = GetDecoration(other.id);
                if (od == null) continue;
                var (ow, oh) = GetSize(od, other.rotation);
                bool overlap = item.x < other.x + ow && item.x + w > other.x &&
                               item.y < other.y + oh && item.y + h > other.y;
                if (overlap) return false;
            }
            return true;
        }

        /// <summary>当前已摆放的指定装饰数量。</summary>
        private int PlacedCount(string id)
        {
            int count = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].id == id) count++;
            }
            return count;
        }

        /// <summary>尺寸（旋转 90/270 时宽高交换，文档 03 §3.8）。</summary>
        private static (int W, int H) GetSize(DecorationConfig d, int rotation)
        {
            bool swap = d.sizeX != d.sizeY && (rotation == 90 || rotation == 270);
            return swap ? (d.sizeY, d.sizeX) : (d.sizeX, d.sizeY);
        }

        /// <summary>把已摆放项摊平成扁平数组（id, x, y, rot, ...），排除指定项（供 Lua 校验透传）。</summary>
        private object[] FlatItems(List<YardItemDto> items, YardItemDto exclude)
        {
            var flat = new object[items.Count * 4];
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (exclude != null && ReferenceEquals(it, exclude)) continue;
                flat[n++] = it.id;
                flat[n++] = it.x;
                flat[n++] = it.y;
                flat[n++] = it.rotation;
            }
            if (n < flat.Length) Array.Resize(ref flat, n);
            return flat;
        }
    }
}
