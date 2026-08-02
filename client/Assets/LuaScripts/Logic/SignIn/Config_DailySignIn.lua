-- Config_DailySignIn.lua
-- 签到配置（文档 11 §11.6），Lua 表驱动，可随版本热更。
-- 奖励发放走服务端每日结算链路（文档 11 §11.7），本表供客户端展示与结算读取。

DailySignIn_Week = {
    cycle = 7,
    rewards = {
        { day = 1, shell = 5 },
        { day = 2, shell = 8 },
        { day = 3, shell = 10, item = "seed_wheat", count = 1 },
        { day = 4, shell = 12 },
        { day = 5, shell = 15, item = "coupon_speed", count = 1 },
        { day = 6, shell = 20, item = "pet_food", count = 2 },
        { day = 7, shell = 30, item = "deco_week", count = 1 },
    },
}

DailySignIn_Month = {
    milestones = {
        { total = 14, shell = 100 },
        { total = 30, shell = 200, item = "deco_month", count = 1 },
    },
}

return {
    DailySignIn_Week = DailySignIn_Week,
    DailySignIn_Month = DailySignIn_Month,
}
