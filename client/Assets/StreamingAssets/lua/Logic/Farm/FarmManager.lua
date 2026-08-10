-- FarmManager.lua
-- 种田逻辑脚本（QQ 农场式，文档 04）：识别种植的作物种类与生长时长、计算作物
-- 结束时间并上报服务器、本地倒计时与成熟提醒。
--
-- 设计（与 MatchMaker.lua 一致的"纯逻辑 + 数据源注入"惯例）：
--   * 模块不直接依赖 WC.* 桥接，网络 / 配置 / 计时器全部经 provider 注入，便于
--     独立测试与热更解耦；
--   * 模块加载时 AutoBind 检测 WC.config / WC.net / WC.timer，桥接存在则自动接线，
--     缺失时（桩模式）模块仍可加载，调用网络 / 计时相关功能返回明确错误。
--
-- 未来田系统接入点：
--   local FarmManager = require("Logic.Farm.FarmManager")
--   FarmManager.OnCropMature = function(cellId, plot) ... end   -- 成熟提醒
--   FarmManager.Plant(0, "C01", function(ok, result) ... end)   -- 种植并上报
--   FarmManager.Init(function(ok, msg) ... end)                 -- 恢复已有种植
--   FarmManager.Tick()                                          -- 无计时器时每帧推进
--
-- 时间约定：Unix 秒（os.time()）；readyAt = now() + growTime(分钟) * 60。

local M = {}

--- 作物成熟回调：function(cellId, plot)。未来 UI 在此挂成熟提醒。
M.OnCropMature = nil

-- 默认数据源（全部可被 SetDataProvider 覆盖）
local defaultProvider = {
    -- 识别作物：cropId -> { id, name, growTime(分钟), ... } | nil
    getCrop = function(cropId) return nil end,
    -- 当前时间（Unix 秒）
    now = function() return os.time() end,
    -- 上报种植：function(req, cb)，cb(resp)；resp = { code=0, data, msg }
    sendPlant = nil,
    -- 拉取已有种植：function(cb)，cb(resp)；resp.data = { plots = {...} } 或数组
    fetchPlots = nil,
    -- 定时器：function(cb, ms) -> handle（毫秒）
    setTimeout = nil,
    -- 取消定时器：function(handle)
    clearTimeout = nil,
    -- 日志
    log = function(...) print(...) end,
}

local provider = {}
for k, v in pairs(defaultProvider) do provider[k] = v end

-- 本地种植状态：cellId -> plot
local plots = {}

local function normalizeCellId(v)
    if type(v) == "number" then return math.floor(v) end
    if type(v) == "string" then
        local n = tonumber(v)
        if n then return math.floor(n) end
    end
    return nil
end

local function clonePlot(p)
    if not p then return nil end
    return {
        cellId = p.cellId,
        cropId = p.cropId,
        cropName = p.cropName,
        growTimeMin = p.growTimeMin,
        readyAt = p.readyAt,
        state = p.state,
    }
end

local function markMature(plot)
    if plot.state == "mature" then return end
    plot.state = "mature"
    plot._timer = nil
    provider.log(string.format("[FarmManager] 作物成熟: 格子 %d %s(%s)",
        plot.cellId, plot.cropId, tostring(plot.cropName)))
    if M.OnCropMature then
        local ok, err = pcall(M.OnCropMature, plot.cellId, clonePlot(plot))
        if not ok then
            provider.log("[FarmManager] OnCropMature 回调异常: " .. tostring(err))
        end
    end
end

local function scheduleMature(plot)
    if plot.state ~= "growing" then return end
    local remainSec = plot.readyAt - provider.now()
    if remainSec <= 0 then
        markMature(plot)
        return
    end
    if not provider.setTimeout then
        -- 无计时器：保持 growing，由外部周期调用 M.Tick() 推进
        return
    end
    plot._timer = provider.setTimeout(function()
        plot._timer = nil
        markMature(plot)
    end, math.ceil(remainSec * 1000))
end

--- 注入数据源（覆盖默认实现；传 nil / 缺字段则保持原值）。
function M.SetDataProvider(p)
    p = p or {}
    for k, v in pairs(defaultProvider) do
        if p[k] ~= nil then provider[k] = p[k] end
    end
end

--- 种植：识别作物种类与时长 -> 计算结束时间 -> 上报服务器 -> 本地登记并倒计时。
-- @param cellId 农田格（int，0 起）
-- @param cropId 作物 ID（Config_Crops.json）
-- @param cb 可选 function(ok, result|msg)
function M.Plant(cellId, cropId, cb)
    cb = cb or function() end
    cellId = normalizeCellId(cellId)
    if cellId == nil then return cb(false, "cellId 非法") end
    if type(cropId) ~= "string" or cropId == "" then
        return cb(false, "cropId 不能为空")
    end

    local existing = plots[cellId]
    if existing and existing.state == "growing" then
        return cb(false, "该农田格已有作物在生长")
    end
    if existing then
        -- 覆盖成熟/过期格：先清掉旧倒计时
        if existing._timer and provider.clearTimeout then
            provider.clearTimeout(existing._timer)
        end
        existing._timer = nil
    end

    -- 识别作物种类与时长
    local crop = provider.getCrop(cropId)
    if not crop then
        return cb(false, "未知作物: " .. cropId)
    end
    local growTimeMin = tonumber(crop.growTime)
    if not growTimeMin or growTimeMin <= 0 then
        return cb(false, "作物生长时长非法: " .. cropId)
    end

    local readyAt = provider.now() + growTimeMin * 60

    local send = provider.sendPlant
    if not send then
        return cb(false, "未接入网络层（sendPlant 未注入）")
    end

    send({ cellId = cellId, cropId = cropId, readyAt = readyAt }, function(resp)
        if not resp or resp.code ~= 0 then
            return cb(false, (resp and resp.msg) or "上报种植失败")
        end
        local plot = {
            cellId = cellId,
            cropId = cropId,
            cropName = crop.name,
            growTimeMin = growTimeMin,
            readyAt = readyAt,
            state = "growing",
        }
        plots[cellId] = plot
        scheduleMature(plot)
        provider.log(string.format("[FarmManager] 种植成功: 格子 %d %s(%s) 结束时间 %d",
            cellId, cropId, tostring(crop.name), readyAt))
        cb(true, clonePlot(plot))
    end)
end

--- 查询单格本地状态（growing / mature）。
function M.GetPlot(cellId)
    cellId = normalizeCellId(cellId)
    if cellId == nil then return nil end
    return clonePlot(plots[cellId])
end

--- 查询全部本地状态（按格子升序）。
function M.GetPlots()
    local list = {}
    for _, p in pairs(plots) do table.insert(list, clonePlot(p)) end
    table.sort(list, function(a, b) return a.cellId < b.cellId end)
    return list
end

--- 从服务器数据恢复种植状态并重建倒计时（登录 / 断线重连后调用）。
-- @param serverPlots 数组：[{ cellId, cropId, readyAt, state }]（可直接传 resp.data.plots）
function M.RestorePlots(serverPlots)
    if type(serverPlots) ~= "table" then return end
    for _, item in ipairs(serverPlots) do
        local cellId = normalizeCellId(item.cellId)
        local readyAt = tonumber(item.readyAt)
        if cellId ~= nil and readyAt then
            local crop = provider.getCrop(item.cropId)
            local plot = {
                cellId = cellId,
                cropId = item.cropId,
                cropName = (crop and crop.name) or item.cropName or item.cropId,
                growTimeMin = (crop and tonumber(crop.growTime)) or 0,
                readyAt = readyAt,
                state = "growing",
            }
            plots[cellId] = plot
            scheduleMature(plot)
        end
    end
end

--- 初始化：拉取服务端已有种植并恢复倒计时。
function M.Init(cb)
    cb = cb or function() end
    local fetch = provider.fetchPlots
    if not fetch then
        return cb(false, "未接入网络层（fetchPlots 未注入）")
    end
    fetch(function(resp)
        if not resp or resp.code ~= 0 then
            return cb(false, (resp and resp.msg) or "拉取种植状态失败")
        end
        local data = resp.data
        if type(data) == "table" and data.plots then
            M.RestorePlots(data.plots)
        elseif type(data) == "table" then
            M.RestorePlots(data)
        end
        cb(true)
    end)
end

--- 收获（本地占位）：清除成熟格子；服务端收获 / 结算留后续接入。
function M.Harvest(cellId, cb)
    cb = cb or function() end
    cellId = normalizeCellId(cellId)
    if cellId == nil then return cb(false, "cellId 非法") end
    local p = plots[cellId]
    if not p then return cb(false, "该农田格没有作物") end
    if p.state ~= "mature" then return cb(false, "作物尚未成熟") end
    if p._timer and provider.clearTimeout then provider.clearTimeout(p._timer) end
    plots[cellId] = nil
    provider.log("[FarmManager] 已收获: 格子 " .. cellId)
    cb(true)
end

--- 推进倒计时（无 setTimeout 注入时由外部每帧调用；有计时器时可省略）。
function M.Tick()
    local now = provider.now()
    for _, p in pairs(plots) do
        if p.state == "growing" and p.readyAt <= now then
            markMature(p)
        end
    end
end

-- 运行时自动接线：WC.* 桥接存在时绑定，缺失时保持纯逻辑可用（桩模式）。
local function AutoBind()
    local bound = false
    if type(WC) == "table" and type(WC.config) == "table" and WC.config.getRowById then
        provider.getCrop = function(cropId)
            return WC.config.getRowById("Crops", cropId)
        end
        bound = true
    end
    if type(WC) == "table" and type(WC.net) == "table" and WC.net.post
        and type(WC.net.API) == "table" and WC.net.API.YardPlant and WC.net.API.YardPlots then
        provider.sendPlant = function(req, cb)
            WC.net.post(WC.net.API.YardPlant, req, cb)
        end
        provider.fetchPlots = function(cb)
            WC.net.get(WC.net.API.YardPlots, nil, cb)
        end
        bound = true
    end
    if type(WC) == "table" and type(WC.timer) == "table" and WC.timer.setTimeout then
        provider.setTimeout = WC.timer.setTimeout
        provider.clearTimeout = WC.timer.clearTimeout
        bound = true
    end
    if bound then
        provider.log("[FarmManager] 已自动接入 WC 桥接")
    end
end
AutoBind()

return M
