-- FarmDebug.lua（开发调试模块，仅供 Play Mode 冒烟验证，不进 HotModules、不随包发布）
-- 用法：LuaManager.DoString("require('Logic.Farm.FarmDebug')") 后调用
--   FarmDebug.Init()                       -- 接线成熟回调并恢复种植
--   FarmDebug.Plant(0, "C01")              -- 种植并上报（需 WC 桥接 + 服务端 + 种子）
--   FarmDebug.Harvest(0)                   -- 收获成熟作物（产量入背包）
--   FarmDebug.Sell("C01", 3)               -- 出售作物换庭贝
--   FarmDebug.Dump()                       -- 打印本地农田状态

local FarmManager = require("Logic.Farm.FarmManager")

local M = {}

function M.Init()
    FarmManager.OnCropMature = function(cellId, plot)
        print(string.format("[FarmDebug] 作物成熟: 格子 %d %s(%s)",
            cellId, plot.cropId, tostring(plot.cropName)))
    end
    FarmManager.Init(function(ok, msg)
        if ok then
            print("[FarmDebug] Init 完成")
        else
            print("[FarmDebug] Init 失败: " .. tostring(msg))
        end
    end)
end

function M.Plant(cellId, cropId)
    FarmManager.Plant(cellId, cropId, function(ok, result)
        if ok then
            print(string.format("[FarmDebug] 种植成功: 格子 %d %s(%s) 结束时间 %d",
                result.cellId, result.cropId, tostring(result.cropName), result.readyAt))
        else
            print("[FarmDebug] 种植失败: " .. tostring(result))
        end
    end)
end

function M.Harvest(cellId)
    FarmManager.Harvest(cellId, function(ok, msg)
        print(ok and "[FarmDebug] 收获成功" or ("[FarmDebug] 收获失败: " .. tostring(msg)))
    end)
end

function M.Sell(itemId, count)
    FarmManager.SellCrop(itemId, count, function(ok, result)
        if ok then
            print(string.format("[FarmDebug] 出售成功: %s x%d shell=%s",
                result.itemId, result.count, tostring(result.shell)))
        else
            print("[FarmDebug] 出售失败: " .. tostring(result))
        end
    end)
end

function M.Dump()
    local plots = FarmManager.GetPlots()
    print("[FarmDebug] 当前农田:")
    for _, p in ipairs(plots) do
        print(string.format("  格子 %d: %s(%s) state=%s readyAt=%d",
            p.cellId, p.cropId, tostring(p.cropName), p.state, p.readyAt))
    end
end

return M
