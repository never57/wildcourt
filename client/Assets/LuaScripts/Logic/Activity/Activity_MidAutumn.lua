-- Activity_MidAutumn.lua
-- 中秋活动配置示例（文档 13 §13.3/13.4）：Lua 驱动，startTime/endTime/type/content，
-- 服务端到点自动激活/关闭（ActivityEndJob，文档 02 §2.4）。
-- 邮券为活动限定货币，仅本系统产出（文档 13 §13.5）。

Activity_MidAutumn = {
    id = "midautumn_2026",
    name = "月圆人团圆",
    startTime = "2026-09-25 00:00:00", -- 服务器时区 UTC+8
    endTime = "2026-10-05 23:59:59",
    type = "festival",
    content = {
        { type = "decoration", itemId = "D_moon",   name = "中秋圆月", price = 300 },
        { type = "decoration", itemId = "D_rabbit", name = "玉兔雕塑", price = 250 },
        { type = "badge", itemId = "B_midautumn", name = "中秋徽章" },
        { type = "task", desc = "在中秋期间访问5个不同省份的庭院", reward = "月饼食谱" },
    },
}

-- 活动注册表：服务端/客户端启动时扫描本目录所有 Activity_*.lua 汇总
Activities = {
    Activity_MidAutumn,
}

return Activities
