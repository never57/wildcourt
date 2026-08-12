-- YardManager.lua
-- 庭院建造（文档 03）纯逻辑层：等轴测网格摆放的容量 / 上限 / 边界 / 重叠校验。
--
-- 设计（与 FarmManager.lua 一致的"纯逻辑 + 数据源注入"惯例）：
--   * 模块不直接依赖 WC.* 桥接，配置查询经 provider 注入，便于独立测试与热更解耦；
--   * 校验函数只接收"扁平简单参数"（数字 / 字符串），由 C# 侧 YardManager 把
--     List<YardItemDto> 摊平后传入，避免在 XLua 桥接层传复杂对象。
--
-- 未来接入点（C# 侧 YardManager.CanPlaceAt / CanMoveTo / GetSize 委托至此）：
--   YardManager.SetDataProvider(p)              -- 注入配置查询 / 日志
--   YardManager.CanPlaceAt(params...) -> ok      -- 放置合法性（容量 + 上限 + 边界 + 重叠）
--   YardManager.CanMoveTo(params...) -> ok       -- 移动合法性（重叠排除自身）
--   YardManager.GetSize(w, h, rotation) -> w, h  -- 旋转宽高交换
--   YardManager.IsInPlaceBounds(...) -> ok       -- 边界判定

local M = {}

-- 默认数据源（全部可被 SetDataProvider 覆盖）
local defaultProvider = {
    -- 读取装饰配置：id -> { sizeX, sizeY, maxCount, rotatable } | nil
    getDecoration = function(id) return nil end,
    -- 日志
    log = function(...) print(...) end,
}

local provider = {}
for k, v in pairs(defaultProvider) do provider[k] = v end

--- 注入数据源（覆盖默认实现；传 nil / 缺字段则保持原值）。
function M.SetDataProvider(p)
    p = p or {}
    for k, v in pairs(defaultProvider) do
        if p[k] ~= nil then provider[k] = p[k] end
    end
end

--- 旋转后的占格尺寸（旋转 90/270 时宽高交换，文档 03 §3.8）。
-- @return 宽, 高
function M.GetSize(sizeX, sizeY, rotation)
    sizeX = tonumber(sizeX) or 0
    sizeY = tonumber(sizeY) or 0
    rotation = tonumber(rotation) or 0
    if sizeX ~= sizeY and (rotation == 90 or rotation == 270) then
        return sizeY, sizeX
    end
    return sizeX, sizeY
end

--- 矩形 (x,y)-(x+w-1, y+h-1) 是否完整落在可放置边界内。
function M.IsInPlaceBounds(x, y, w, h, xMin, yMin, xMax, yMax)
    x = tonumber(x); y = tonumber(y)
    w = tonumber(w); h = tonumber(h)
    xMin = tonumber(xMin); yMin = tonumber(yMin)
    xMax = tonumber(xMax); yMax = tonumber(yMax)
    if x == nil or y == nil or w == nil or h == nil then return false end
    return x >= xMin and y >= yMin and (x + w - 1) <= xMax and (y + h - 1) <= yMax
end

-- 两矩形是否重叠（等轴测网格按 AABB 判定，与 C# 原实现一致）
local function overlap(x, y, w, h, ox, oy, ow, oh)
    return x < ox + ow and x + w > ox and y < oy + oh and y + h > oy
end

--- 放置合法性（容量 + 拥有上限 + 边界 + 重叠）。
-- 参数（扁平，全部标量，便于 XLua CallFunc 透传）：
--   decorationId   待放装饰 id
--   x, y           待放坐标
--   rotation       旋转角
--   itemCount      当前已摆放总数（用于容量判定）
--   cap            容量上限 decorationLimit
--   owned          该装饰背包拥有量
--   placed         该装饰当前已摆放数
--   xMin/yMin/xMax/yMax  可放置边界
--   其余为已摆放项扁平数据，按 4 个一组：id1,x1,y1,rot1, id2,x2,y2,rot2, ...
-- @return ok, 失败原因（ok=false 时非空）
function M.CanPlaceAt(decorationId, x, y, rotation, itemCount, cap, owned, placed, xMin, yMin, xMax, yMax, ...)
    local d = provider.getDecoration(decorationId)
    if not d then return false, "装饰不存在" end

    itemCount = tonumber(itemCount) or 0
    cap = tonumber(cap) or 0
    if itemCount >= cap then return false, "容量已满" end

    owned = tonumber(owned) or 0
    local maxCount = tonumber(d.maxCount) or 0
    local limit = math.min(owned, maxCount)
    placed = tonumber(placed) or 0
    if placed >= limit then return false, "已达拥有上限" end

    local w, h = M.GetSize(d.sizeX, d.sizeY, rotation)
    if not M.IsInPlaceBounds(x, y, w, h, xMin, yMin, xMax, yMax) then
        return false, "超出可放置范围"
    end

    local flat = { ... }
    x = tonumber(x); y = tonumber(y)
    for i = 1, #flat, 4 do
        local oid = flat[i]
        local ox, oy, orot = tonumber(flat[i + 1]), tonumber(flat[i + 2]), tonumber(flat[i + 3])
        if oid and ox and oy then
            local od = provider.getDecoration(oid)
            if od then
                local ow, oh = M.GetSize(od.sizeX, od.sizeY, orot)
                if overlap(x, y, w, h, ox, oy, ow, oh) then
                    return false, "与其它装饰重叠"
                end
            end
        end
    end
    return true
end

--- 移动合法性（推荐入口）：显式传入被移动项 id，避免依赖扁平数组下标。
--   id            被移动装饰 id
--   x, y          目标坐标
--   rotation      旋转角
--   xMin/yMin/xMax/yMax  边界
--   其余为已摆放项扁平数据（4 个一组：id,x,y,rot），被移动项应已排除（由 C# 侧排除自身）。
function M.CanMoveToById(id, x, y, rotation, xMin, yMin, xMax, yMax, ...)
    local d = provider.getDecoration(id)
    if not d then return false, "装饰不存在" end
    local w, h = M.GetSize(d.sizeX, d.sizeY, rotation)
    if not M.IsInPlaceBounds(x, y, w, h, xMin, yMin, xMax, yMax) then
        return false, "超出可放置范围"
    end

    x = tonumber(x); y = tonumber(y)
    local flat = { ... }
    for i = 1, #flat, 4 do
        local oid = flat[i]
        local ox, oy, orot = tonumber(flat[i + 1]), tonumber(flat[i + 2]), tonumber(flat[i + 3])
        if oid and ox and oy then
            local od = provider.getDecoration(oid)
            if od then
                local ow, oh = M.GetSize(od.sizeX, od.sizeY, orot)
                if overlap(x, y, w, h, ox, oy, ow, oh) then
                    return false, "与其它装饰重叠"
                end
            end
        end
    end
    return true
end

return M
