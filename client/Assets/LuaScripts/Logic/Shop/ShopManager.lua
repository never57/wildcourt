-- ShopManager.lua
-- 建筑系统商店（文档 03 §3.2、09）纯逻辑层：货架分类过滤 + 购买请求构造 + 购买校验。
--
-- 设计（与 FarmManager.lua / YardManager.lua 一致的"纯逻辑 + 数据源注入"惯例）：
--   * 模块不直接依赖 WC.* 桥接，网络 / 配置经 provider 注入，便于独立测试与热更解耦；
--   * 只负责"货架过滤 / 分类 / 购买请求构造"等无 Unity 依赖的纯逻辑，
--     UI 外壳（YardPanel / YardCell）与预制体预加载（LoginController 注入 YardPanelPrefab）保持 C# 不动。
--
-- 未来接入点（C# 侧 ShopManager / YardPanel 委托至此）：
--   ShopManager.SetDataProvider(p)            -- 注入购买上报 / 日志
--   ShopManager.FilterByCategory(rows, cat)   -- 货架分类过滤（"全部" 返回全部）
--   ShopManager.CanBuy(owned, maxCount, placed) -> ok, 原因  -- 是否还能购买/建造
--
-- 注意：预加载（YardPanelPrefab 引用注入）是纯 C# 链路，与 Lua 无关，不会因本模块下沉而中断。

local M = {}

-- 默认数据源（全部可被 SetDataProvider 覆盖）
local defaultProvider = {
    -- 购买上报：function(itemId, count, currency, cb)，cb(resp)；resp = { code=0, data, msg }
    sendBuy = nil,
    -- 日志
    log = function(...) print(...) end,
    -- 发布事件：function(name, payload)
    publish = function() end,
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

--- 分类匹配：targetCategory 为 "全部"/空/nil 时放行，否则要求 itemCategory 精确匹配。
-- @param itemCategory 商品分类
-- @param targetCategory 目标分类（"全部" 表示不过滤）
-- @return bool 是否匹配
function M.MatchCategory(itemCategory, targetCategory)
    if targetCategory == nil or targetCategory == "" or targetCategory == "全部" then
        return true
    end
    return itemCategory == targetCategory
end

--- 货架分类过滤：cat 为 "全部" 或空时返回全部，否则按 category 精确匹配。
-- @param rows 数组：[{ id, name, category, ... }]（Lua table 数组）
-- @param cat 分类名（"全部" / "景观" / "家具" ...）
-- @return 过滤后的数组
function M.FilterByCategory(rows, cat)
    if type(rows) ~= "table" then return {} end
    local out = {}
    local n = 0
    for i = 1, #rows do
        local r = rows[i]
        if r and M.MatchCategory(r.category, cat) then
            n = n + 1
            out[n] = r
        end
    end
    return out
end

--- 是否还能购买 / 建造该装饰（剩余 = maxCount - owned，已建造数不计入拥有上限）。
-- @param owned 背包拥有量
-- @param maxCount 可拥有上限
-- @param placed 已摆放数量（预留，当前规则只看 owned/maxCount）
-- @return ok, 原因（ok=false 时原因非空）
function M.CanBuy(owned, maxCount, placed)
    owned = tonumber(owned) or 0
    maxCount = tonumber(maxCount) or 0
    if maxCount <= 0 then return false, "该装饰不可购买" end
    local remain = maxCount - owned
    if remain <= 0 then return false, "已达拥有上限" end
    return true
end

--- 购买入口：构造请求并上报（currency 默认 shell）。
-- @param itemId 商品 id
-- @param count 数量
-- @param currency 货币（shell / coupon）
-- @param cb function(resp)（resp = { code, data, msg }）
function M.Buy(itemId, count, currency, cb)
    cb = cb or function() end
    local function fail(msg)
        provider.publish("Shop.BuyResult", { itemId = itemId, ok = false, msg = msg })
        return cb({ code = -1, data = nil, msg = msg })
    end
    if type(itemId) ~= "string" or itemId == "" then
        return fail("itemId 不能为空")
    end
    count = tonumber(count)
    if not count or count <= 0 then
        return fail("购买数量必须大于 0")
    end
    currency = currency or "shell"
    local send = provider.sendBuy
    if not send then
        return fail("未接入网络层（sendBuy 未注入）")
    end
    send(itemId, count, currency, function(resp)
        if not resp or resp.code ~= 0 then
            return fail((resp and resp.msg) or "购买失败")
        end
        provider.log(string.format("[ShopManager] 购买成功: %s x%d (%s)", itemId, count, currency))
        provider.publish("Shop.BuyResult", {
            itemId = itemId, count = count, ok = true,
            shell = (resp.data and resp.data.shell) or 0,
            coupon = (resp.data and resp.data.coupon) or 0,
            msg = "",
        })
        cb(resp)
    end)
end

return M
