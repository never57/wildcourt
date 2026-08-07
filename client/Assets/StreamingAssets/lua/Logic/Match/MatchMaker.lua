-- MatchMaker.lua
-- 漫游匹配算法（文档 07 §7.7/7.8），客户端与服务端复用同一份逻辑。
-- 权重：好客值 60% / 随机 20% / 首访 10% / 好友 10%。
--
-- 用法：
--   服务端：MatchMaker.SetDataProvider({ GetCityPool = ..., GetProvincePool = ..., IsUserAvailable = ..., GetNPC = ... })
--           其中 GetCityPool/GetProvincePool 封装 Redis ZRevRange("match_pool:{provinceId}[:{cityId}]")
--   客户端：同上注入本地数据源（预览用），正式匹配以服务端 MatchService 为准。

local M = {}

local function in_list(list, item)
    for _, v in ipairs(list) do
        if v == item then return true end
    end
    return false
end

--- 数据源注入（隔离 Redis 依赖，便于两端复用与单测）
function M.SetDataProvider(provider)
    M.provider = provider or {}
end

local function provider()
    return M.provider or {}
end

--- 按权重为目标省匹配庭院
-- @param provinceId 目标省份
-- @param cityId     目标城市（nil 则不限城市）
-- @param count      需要匹配的庭院数量
-- @param hospitalityValues 该省好客值 Sorted Set（预留，排序由 Redis ZRevRange 完成）
-- @return { userId, ... } 目标列表（不足用 NPC 庭院补位）
function M.FindTargets(provinceId, cityId, count, hospitalityValues)
    local p = provider()
    local candidates = {}

    -- 1. 指定城市：先从城市池取（多取 3 倍给过滤留余量）
    if cityId and p.GetCityPool then
        candidates = p.GetCityPool(provinceId, cityId, count * 3) or {}
    end

    -- 2. 城市不够或未指定：从省池补充（去重）
    if #candidates < count and p.GetProvincePool then
        local provinceCands = p.GetProvincePool(provinceId, count * 3) or {}
        for _, c in ipairs(provinceCands) do
            if not in_list(candidates, c) then
                table.insert(candidates, c)
            end
        end
    end

    -- 3. 过滤不可用（离线/食物盆空/被屏蔽）
    local targets = {}
    for _, userId in ipairs(candidates) do
        local ok = true
        if p.IsUserAvailable then
            ok = p.IsUserAvailable(userId)
        end
        if ok then
            table.insert(targets, userId)
            if #targets >= count then break end
        end
    end

    -- 4. 不足时填充 NPC 庭院
    if p.GetNPC then
        while #targets < count do
            table.insert(targets, p.GetNPC(provinceId))
        end
    end

    return targets
end

return M
