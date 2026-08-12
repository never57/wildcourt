-- 数据库初始化脚本（骨架，待实现）
-- 表结构见文档 15.3：users / pets / inventories / yards / letters / friends /
-- nonfiction_collections / badges / transactions / match_pools / operation_logs /
-- daily_settlements / sign_in_records
CREATE DATABASE IF NOT EXISTS `wildcourt` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `wildcourt`;

-- sign_in_records：每日签到记录（EF 迁移 AddSignInRecords 同步）
CREATE TABLE IF NOT EXISTS `sign_in_records` (
    `record_id` BIGINT NOT NULL AUTO_INCREMENT,
    `user_id` BIGINT NOT NULL,
    `sign_date` DATE NOT NULL,
    `created_at` DATETIME(6) NOT NULL,
    PRIMARY KEY (`record_id`),
    UNIQUE KEY `uk_sign_in_user_date` (`user_id`, `sign_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- TODO: 逐表 CREATE TABLE（与 WildCourt.Data/Entities + EF Core 迁移保持一致）
