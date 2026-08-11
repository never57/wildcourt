import { request } from './http'

// ================= 类型定义 =================

export interface AdminProfile {
  adminId: number
  username: string
  displayName: string
  lastLoginAt?: string
}

export interface AdminItem {
  adminId: number
  username: string
  displayName: string
  isActive: boolean
  createdAt: string
  lastLoginAt?: string
}

export interface DashboardSummary {
  totalPlayers: number
  todayNewPlayers: number
  onlinePlayers: number
  todayActivePlayers: number
  totalShell: number
  totalCoupon: number
  registrationTrend: { date: string; count: number }[]
}

export interface ContainerInfo {
  containerId: string
  name: string
  image: string
  state: string
  status: string
  cpuPercent?: number | null
  memUsage?: number | null
  memLimit?: number | null
  ports: string
  runningFor: string
}

export interface ServerOverview {
  hostName: string
  dockerVersion: string
  loadAvg1: number
  loadAvg5: number
  loadAvg15: number
  memTotal: number
  memUsed: number
  memUsagePercent: number
  diskUsed: number
  diskTotal: number
  diskUsagePercent: number
  diskMountPoint: string
  containers: ContainerInfo[]
  dockerDiskSummary: string
}

export interface PlayerItem {
  userId: number
  username: string
  nickname: string
  avatarUrl?: string
  level: number
  shell: number
  coupon: number
  exp: number
  isBanned: boolean
  createdAt: string
  lastLogin?: string
}

export interface PagedResult<T> {
  items: T[]
  total: number
}

export interface PetItem {
  petId: number
  type: string
  level: number
  exp: number
  status: string
}

export interface InventoryItem {
  itemId: string
  count: number
}

export interface PlayerDetail {
  player: PlayerItem
  pets: PetItem[]
  inventory: InventoryItem[]
  friendCount: number
}

export interface ReviewQueueItem {
  letterId: number
  fromUser: number
  fromNickname: string
  toUser: number
  toNickname: string
  content: string
  itemId?: string
  status: string
  createdAt: string
}

export interface Announcement {
  announcementId: number
  title: string
  content: string
  status: string
  creatorName: string
  createdAt: string
  publishedAt?: string
}

export interface DeployStatus {
  enabled: boolean
  hostCodeRoot: string
  composeFile: string
  status: string
  building: boolean
  lastDeployAt?: string
  lastDeployResult: string
}

export interface ExecuteSqlResult {
  statementCount: number
  affectedRows: number
  durationMs: number
  message: string
}

export interface DatabaseInfo {
  name: string
}

export interface TableInfo {
  name: string
  comment: string
  rows: number
}

export interface ColumnInfo {
  name: string
  columnType: string
  nullable: boolean
  isPrimaryKey: boolean
  defaultValue?: string | null
  comment: string
}

export interface TableDataResult {
  columns: ColumnInfo[]
  rows: Record<string, unknown>[]
  total: number
  page: number
  pageSize: number
}

export interface RowChangeRequest {
  database?: string
  table: string
  values: Record<string, unknown>
  primaryKey?: string
  primaryKeyValue?: unknown
}

// ================= 认证 =================

export const authApi = {
  login: (username: string, password: string) =>
    request<{ token: string; username: string; displayName: string; expireAt: string }>({
      url: '/auth/login',
      method: 'post',
      data: { username, password },
    }),
  profile: () => request<AdminProfile>({ url: '/auth/profile' }),
  changePassword: (oldPassword: string, newPassword: string) =>
    request<void>({ url: '/auth/change-password', method: 'post', data: { oldPassword, newPassword } }),
  listAdmins: () => request<AdminItem[]>({ url: '/admins' }),
  createAdmin: (data: { username: string; password: string; displayName: string }) =>
    request<void>({ url: '/admins', method: 'post', data }),
  setActive: (adminId: number, active: boolean) =>
    request<void>({ url: `/admins/${adminId}/active/${active}`, method: 'post' }),
  resetPassword: (adminId: number, newPassword: string) =>
    request<void>({ url: `/admins/${adminId}/reset-password`, method: 'post', data: { newPassword } }),
  removeAdmin: (adminId: number) =>
    request<void>({ url: `/admins/${adminId}`, method: 'delete' }),
}

// ================= 仪表盘 =================

export const dashboardApi = {
  summary: () => request<DashboardSummary>({ url: '/dashboard/summary' }),
}

// ================= 服务器监控 =================

export const serverApi = {
  overview: () => request<ServerOverview>({ url: '/server/overview' }),
  containers: () => request<ContainerInfo[]>({ url: '/server/containers' }),
  logs: (nameOrId: string, tail = 300) =>
    request<{ containerName: string; log: string }>({ url: `/server/containers/${encodeURIComponent(nameOrId)}/logs`, params: { tail } }),
  restart: (nameOrId: string) =>
    request<void>({ url: `/server/containers/${encodeURIComponent(nameOrId)}/restart`, method: 'post' }),
}

// ================= 玩家管理 =================

export const playerApi = {
  list: (params: { keyword?: string; page?: number; pageSize?: number }) =>
    request<PagedResult<PlayerItem>>({ url: '/players', params }),
  detail: (userId: number) => request<PlayerDetail>({ url: `/players/${userId}` }),
  updateCurrency: (userId: number, data: { shell?: number; coupon?: number; level?: number; exp?: number }) =>
    request<void>({ url: `/players/${userId}/currency`, method: 'put', data }),
  updateProfile: (userId: number, data: { nickname?: string; avatarUrl?: string }) =>
    request<void>({ url: `/players/${userId}/profile`, method: 'put', data }),
  ban: (userId: number) => request<{ changed: boolean }>({ url: `/players/${userId}/ban`, method: 'post' }),
  unban: (userId: number) => request<{ changed: boolean }>({ url: `/players/${userId}/unban`, method: 'post' }),
}

// ================= 运营审核 =================

export const reviewApi = {
  queue: () => request<ReviewQueueItem[]>({ url: '/audit-queue' }),
  review: (letterId: number, approve: boolean) =>
    request<void>({ url: '/letter/review', method: 'post', data: { letterId, approve } }),
}

// ================= 公告管理 =================

export const announcementApi = {
  list: () => request<Announcement[]>({ url: '/announcements' }),
  create: (data: { title: string; content: string; status: string }) =>
    request<{ announcementId: number }>({ url: '/announcements', method: 'post', data }),
  update: (id: number, data: { title: string; content: string; status: string }) =>
    request<void>({ url: `/announcements/${id}`, method: 'put', data }),
  remove: (id: number) => request<void>({ url: `/announcements/${id}`, method: 'delete' }),
  publish: (id: number) => request<void>({ url: `/announcements/${id}/publish`, method: 'post' }),
}

// ================= 数据库管理 =================

export const databaseApi = {
  executeSql: (sql: string) =>
    request<ExecuteSqlResult>({ url: '/database/execute-sql', method: 'post', data: { sql } }),
  uploadSql: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<ExecuteSqlResult>({ url: '/database/upload-sql', method: 'post', data: form })
  },
  listDatabases: () => request<DatabaseInfo[]>({ url: '/database/databases' }),
  listTables: (database: string) =>
    request<TableInfo[]>({ url: '/database/tables', params: { database } }),
  getTableData: (database: string, table: string, page = 1, pageSize = 50) =>
    request<TableDataResult>({ url: '/database/table-data', params: { database, table, page, pageSize } }),
  updateRow: (data: RowChangeRequest) =>
    request<number>({ url: '/database/row', method: 'put', data }),
  insertRow: (data: RowChangeRequest) =>
    request<number>({ url: '/database/row', method: 'post', data }),
  deleteRow: (data: RowChangeRequest) =>
    request<number>({ url: '/database/row', method: 'delete', data }),
}

// ================= 服务端部署 =================

export interface FileEntry {
  name: string
  path: string
  type: 'dir' | 'file'
  size: number
  modifiedAt: string
}

export interface FileContent {
  path: string
  content: string
  size: number
  encoding: string
  modifiedAt: string
}

export const deployApi = {
  status: () => request<DeployStatus>({ url: '/deploy/status' }),
  logs: (count = 200) => request<string[]>({ url: '/deploy/logs', params: { count } }),
  upload: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<{ files: number; message: string }>({ url: '/deploy/upload', method: 'post', data: form })
  },
  build: (noCache = false, service?: string) =>
    request<{ started: boolean; message: string }>({ url: '/deploy/build', method: 'post', data: { noCache, service } }),
  listFiles: (path = '') => request<FileEntry[]>({ url: '/deploy/files', params: { path } }),
  uploadFile: (dirPath: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    form.append('path', dirPath)
    return request<{ path: string; size: number; message: string }>({ url: '/deploy/file', method: 'post', data: form })
  },
  deleteFile: (path: string) => request<{ message: string }>({ url: '/deploy/file', params: { path }, method: 'delete' }),
  mkdir: (path: string) => request<{ message: string }>({ url: '/deploy/mkdir', method: 'post', data: { path } }),
  getFileContent: (path: string) => request<FileContent>({ url: '/deploy/file/content', params: { path } }),
  saveFileContent: (path: string, content: string, encoding = 'utf-8') =>
    request<{ path: string; size: number; message: string }>({
      url: '/deploy/file/content',
      method: 'put',
      data: { path, content, encoding },
    }),
}
