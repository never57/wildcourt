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

-- friends：好友关系表（EF 迁移 InitSchema 同步）。A→B 与 B→A 各存一行（双向）。
CREATE TABLE IF NOT EXISTS `friends` (
    `user_id` BIGINT NOT NULL COMMENT '用户 ID',
    `friend_id` BIGINT NOT NULL COMMENT '好友用户 ID',
    `created_at` DATETIME(6) NOT NULL COMMENT '成为好友时间',
    PRIMARY KEY (`user_id`, `friend_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- friend_requests：好友申请表（EF 迁移 AddFriendAndChat 同步）。
-- status：pending（待处理）/ accepted（已接受）/ rejected（已拒绝）。
CREATE TABLE IF NOT EXISTS `friend_requests` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `from_user_id` BIGINT NOT NULL COMMENT '申请人用户 ID',
    `to_user_id` BIGINT NOT NULL COMMENT '被申请人用户 ID',
    `status` VARCHAR(16) NOT NULL DEFAULT 'pending',
    `created_at` DATETIME(6) NOT NULL,
    PRIMARY KEY (`id`),
    KEY `idx_friend_requests_to_status` (`to_user_id`, `status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- chat_messages：好友私聊消息表（EF 迁移 AddFriendAndChat 同步，支持离线消息 + 历史）。
CREATE TABLE IF NOT EXISTS `chat_messages` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `from_user_id` BIGINT NOT NULL COMMENT '发送者用户 ID',
    `to_user_id` BIGINT NOT NULL COMMENT '接收者用户 ID',
    `content` VARCHAR(1000) NOT NULL,
    `is_read` TINYINT(1) NOT NULL DEFAULT 0,
    `created_at` DATETIME(6) NOT NULL,
    PRIMARY KEY (`id`),
    KEY `idx_chat_messages_to_read` (`to_user_id`, `is_read`),
    KEY `idx_chat_messages_from_to` (`from_user_id`, `to_user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- TODO: 逐表 CREATE TABLE（与 WildCourt.Data/Entities + EF Core 迁移保持一致）
