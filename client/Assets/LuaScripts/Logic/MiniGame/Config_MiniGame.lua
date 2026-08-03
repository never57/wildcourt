-- Config_MiniGame.lua
-- 小游戏配置（文档 10 §10.7），可随版本热更（文档 14 §14.1）。
-- 评级映射：S/A/B/C → 庭贝 50/35/20/10；每日上限 200（服务端二次验分，文档 10 §10.8）。

MiniGame_List = {
    { id = "puzzle", name = "拼图",   type = "puzzle",   reward = { S = 50, A = 35, B = 20, C = 10 } },
    { id = "spot",   name = "找茬",   type = "spot",     reward = { S = 50, A = 35, B = 20, C = 10 } },
    { id = "memory", name = "记忆翻牌", type = "memory",  reward = { S = 50, A = 35, B = 20, C = 10 } },
    { id = "catch",  name = "接信",   type = "catch",    reward = { S = 50, A = 35, B = 20, C = 10 } },
    { id = "rhythm", name = "节奏点击", type = "rhythm",  reward = { S = 50, A = 35, B = 20, C = 10 } },
}

MiniGame_DailyCap = 200 -- 每日庭贝上限（控通胀）

return {
    MiniGame_List = MiniGame_List,
    MiniGame_DailyCap = MiniGame_DailyCap,
}
