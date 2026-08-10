-- FarmManagerTest.lua（开发自测脚本）
-- 用法：需要 Lua 5.3+ 解释器（当前开发环境未安装，后续接入 CI / 本地环境后运行）：
--   cd client/Assets/LuaScripts/Logic/Farm
--   lua FarmManagerTest.lua
-- 覆盖：识别作物与时长、结束时间计算、上报请求体、未知作物拒绝、重复种植拒绝、
--       倒计时到期成熟回调、RestorePlots 过期立即成熟、Init 恢复、Harvest 本地清除。

local scriptDir = arg and arg[0] and arg[0]:match("^(.*)[/\\]") or "."
package.path = scriptDir .. "?.lua;" .. package.path

local FarmManager = require("Logic.Farm.FarmManager")

-- ---- fake provider ----
local crops = {
    C01 = { id = "C01", name = "小麦", growTime = 5 },
    C02 = { id = "C02", name = "玉米", growTime = 10 },
    C03 = { id = "C03", name = "番茄", growTime = 20 },
    C04 = { id = "C04", name = "草莓", growTime = 40 },
}
local fakeNow = 1000000
local timers = {}
local nextTimerId = 1
local sentRequests = {}
local fetched = { code = 0, data = { plots = {} }, msg = "" }
local matureEvents = {}

local function countTimers()
    local n = 0
    for _ in pairs(timers) do n = n + 1 end
    return n
end

local function fireTimers()
    local due = {}
    for h, t in pairs(timers) do
        if t.due <= fakeNow then due[#due + 1] = h end
    end
    table.sort(due)
    for _, h in ipairs(due) do
        local t = timers[h]
        if t then
            timers[h] = nil
            t.cb()
        end
    end
end

FarmManager.SetDataProvider({
    getCrop = function(id) return crops[id] end,
    now = function() return fakeNow end,
    sendPlant = function(req, cb)
        sentRequests[#sentRequests + 1] = req
        cb({ code = 0, data = req, msg = "" })
    end,
    fetchPlots = function(cb)
        cb(fetched)
    end,
    setTimeout = function(cb, ms)
        local h = nextTimerId
        nextTimerId = nextTimerId + 1
        timers[h] = { cb = cb, due = fakeNow + ms / 1000 }
        return h
    end,
    clearTimeout = function(h) timers[h] = nil end,
    log = function() end,
})
FarmManager.OnCropMature = function(cellId, plot)
    matureEvents[#matureEvents + 1] = { cellId = cellId, plot = plot }
end

-- ---- 断言工具 ----
local checks, failures = 0, 0
local function ok(cond, msg)
    checks = checks + 1
    if cond then
        print("PASS: " .. msg)
    else
        failures = failures + 1
        print("FAIL: " .. msg)
    end
end

-- ---- 1. 识别作物与时长 + 结束时间计算 + 上报请求体 ----
local plantResult, plantMsg
FarmManager.Plant(0, "C01", function(r, m) plantResult, plantMsg = r, m end)
ok(plantResult == true, "种植成功回调")
ok(plantMsg and plantMsg.cropId == "C01" and plantMsg.cropName == "小麦", "识别作物种类")
ok(plantMsg and plantMsg.growTimeMin == 5, "识别生长时长(分钟)")
ok(plantMsg and plantMsg.readyAt == fakeNow + 5 * 60, "结束时间 = now + growTime*60")
ok(sentRequests[1] and sentRequests[1].cellId == 0 and sentRequests[1].cropId == "C01"
    and sentRequests[1].readyAt == fakeNow + 300, "上报请求体 {cellId, cropId, readyAt}")
local plot0 = FarmManager.GetPlot(0)
ok(plot0 and plot0.state == "growing", "本地状态 growing")

-- ---- 2. 未知作物拒绝 ----
local r2, m2
FarmManager.Plant(1, "X99", function(r, m) r2, m2 = r, m end)
ok(r2 == false and type(m2) == "string" and m2:find("未知作物"), "未知作物拒绝")
ok(FarmManager.GetPlot(1) == nil, "未知作物不产生本地状态")

-- ---- 3. 重复种植拒绝 ----
local r3, m3
FarmManager.Plant(0, "C02", function(r, m) r3, m3 = r, m end)
ok(r3 == false and type(m3) == "string" and m3:find("已有作物"), "同格重复种植拒绝")

-- ---- 4. 倒计时到期成熟回调 ----
ok(#matureEvents == 0, "成熟前无回调")
fakeNow = plot0.readyAt
FarmManager.Tick() -- 无计时器路径 / 兜底
ok(FarmManager.GetPlot(0).state == "mature", "到期后状态 mature(Tick 路径)")
ok(#matureEvents == 1 and matureEvents[1].cellId == 0, "成熟回调触发(格子0)")
ok(matureEvents[1].plot.state == "mature" and matureEvents[1].plot.cropId == "C01", "回调携带 plot 信息")

-- 计时器路径：重新种植并推进 fakeNow 触发 setTimeout
local r4
FarmManager.Plant(0, "C02", function(r) r4 = r end)
ok(r4 == true and countTimers() == 1, "种植成功并创建倒计时(旧计时器已清理)")
local timerHandle = next(timers)
ok(timers[timerHandle].due == fakeNow + 10 * 60, "倒计时时长 = growTime*60")
fakeNow = timers[timerHandle].due
fireTimers()
ok(FarmManager.GetPlot(0).state == "mature", "setTimeout 到期成熟")
ok(#matureEvents == 2, "计时器路径成熟回调")

-- ---- 5. RestorePlots 过期立即成熟 ----
fakeNow = 2000000
fetched = { code = 0, data = { plots = {
    { cellId = 2, cropId = "C03", readyAt = 1900000, state = "mature" },  -- 已过期
    { cellId = 3, cropId = "C04", readyAt = 2100000, state = "growing" }, -- 未到期
} }, msg = "" }
FarmManager.RestorePlots(fetched.data.plots)
ok(FarmManager.GetPlot(2) and FarmManager.GetPlot(2).state == "mature", "Restore 过期格立即成熟")
ok(FarmManager.GetPlot(3) and FarmManager.GetPlot(3).state == "growing", "Restore 未过期格保持 growing")
ok(FarmManager.GetPlot(2).cropName == "番茄", "Restore 识别作物名")

-- ---- 6. Init 拉取 + Harvest 本地清除 ----
local initOk, initMsg
FarmManager.Init(function(r, m) initOk, initMsg = r, m end)
ok(initOk == true, "Init 成功回调")
ok(FarmManager.GetPlot(2) and FarmManager.GetPlot(2).state == "mature", "Init 恢复过期格成熟")

local hOk, hMsg
FarmManager.Harvest(3, function(r, m) hOk, hMsg = r, m end)
ok(hOk == false and hMsg:find("尚未成熟"), "未成熟 Harvest 拒绝")
FarmManager.Harvest(2, function(r, m) hOk, hMsg = r, m end)
ok(hOk == true, "成熟 Harvest 成功")
ok(FarmManager.GetPlot(2) == nil, "Harvest 后本地清除")

-- ---- 汇总 ----
print(string.format("\n共 %d 项断言，失败 %d 项", checks, failures))
if failures > 0 then os.exit(1) end
