/**
 * 数据库表/字段中文注释映射（前端硬编码字典）。
 * 数据库未写 COMMENT 时，数据库管理页用它兜底展示中文名。
 */

// 表名 → 中文注释
export const TABLE_ZH: Record<string, string> = {
  admins: '运营后台管理员',
  announcements: '游戏内公告',
  farm_plots: '农田格种植记录',
  friends: '好友关系',
  inventories: '玩家背包',
  letters: '玩家信件',
  operation_logs: '管理员操作审计日志',
  pets: '宠物',
  sign_in_records: '每日签到记录',
  steal_records: '偷菜记录',
  transactions: '交易记录',
  users: '玩家用户',
  yards: '玩家庭院',
  // EF 迁移内部表
  __efmigrationshistory: 'EF 迁移历史',
}

// 字段名 → 中文注释（通用，跨表同名字段语义一致）
export const COLUMN_ZH: Record<string, string> = {
  // 通用
  user_id: '用户ID',
  created_at: '创建时间',
  updated_at: '更新时间',
  status: '状态',
  level: '等级',
  exp: '经验',
  count: '数量',
  type: '类型',
  content: '内容',
  title: '标题',

  // admins / 管理员
  admin_id: '管理员ID',
  username: '登录账号',
  password_hash: '密码哈希',
  display_name: '显示名称',
  role: '角色',
  is_active: '是否启用',
  last_login_at: '最后登录时间',
  two_factor_enabled: '是否启用双因素认证',
  two_factor_secret: '双因素密钥',

  // users / 玩家
  nickname: '昵称',
  avatar_url: '头像URL',
  province_id: '省份ID',
  city_id: '城市ID',
  shell: '庭贝',
  coupon: '邮券',
  is_banned: '是否封禁',
  last_login: '最后登录时间',

  // announcements / 公告
  announcement_id: '公告ID',
  created_by: '创建人管理员ID',
  published_at: '发布时间',

  // letters / 信件
  letter_id: '信件ID',
  from_user: '发件人用户ID（0=官方）',
  to_user: '收件人用户ID',
  attachments: '附件列表（JSON）',
  item_id: '道具/非遗ID',
  reward_type: '附件类型',
  reward_count: '附件数量',

  // operation_logs / 操作日志
  log_id: '日志ID',
  admin_name: '管理员名',
  action: '操作类型',
  method: 'HTTP方法',
  path: '请求路径',
  details: '请求参数摘要',
  ip_address: '客户端IP',
  status_code: '响应状态码',
  duration_ms: '耗时（毫秒）',

  // farm_plots / 农田
  cell_id: '农田格编号',
  crop_id: '作物ID',
  planted_at: '种植时间',
  ready_at: '成熟时间',
  state: '生长状态',
  stolen_count: '被偷取数量',

  // friends / 好友
  friend_id: '好友ID',

  // inventories / 背包
  // (item_id / count 已在上方通用)

  // pets / 宠物
  pet_id: '宠物ID',

  // sign_in_records / 签到
  record_id: '记录ID',
  sign_date: '签到日期',

  // steal_records / 偷菜
  stealer_user_id: '偷菜者ID',
  target_user_id: '被偷玩家ID',
  stolen_at: '偷取时间',

  // transactions / 交易
  trans_id: '交易ID',
  price: '价格',

  // yards / 庭院
  layout_json: '布局JSON',

  // EF 迁移历史
  migration_id: '迁移ID',
  product_version: 'EF 版本',
}

// 表名 → 中文名（用于下拉/树节点直接显示）
export function tableZh(name: string): string {
  return TABLE_ZH[name] || name
}

// 字段名 → 中文名（含当前表上下文可选）
export function columnZh(name: string): string {
  return COLUMN_ZH[name] || name
}
