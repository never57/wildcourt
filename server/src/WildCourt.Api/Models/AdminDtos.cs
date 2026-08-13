using WildCourt.Data.Entities;

namespace WildCourt.Api.Models;

// ============ 认证 ============

public class AdminLoginRequest
{
    public string Username { get; set; } = string.Empty;

    /// <summary>客户端 SHA256 hex（与玩家登录约定一致）。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>验证码 ID（先调 /auth/captcha 获取）。</summary>
    public string? CaptchaId { get; set; }

    /// <summary>验证码内容。</summary>
    public string? CaptchaCode { get; set; }

    /// <summary>双因素验证（第二步时传）。</summary>
    public string? TotpCode { get; set; }

    /// <summary>双因素登录的临时票据（第二步时传）。</summary>
    public string? TotpTicket { get; set; }
}

public class AdminLoginResponse
{
    public string Token { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime ExpireAt { get; set; }

    /// <summary>是否需要双因素第二步验证。</summary>
    public bool NeedTotp { get; set; }

    /// <summary>双因素临时票据（NeedTotp=true 时返回，5 分钟有效）。</summary>
    public string? TotpTicket { get; set; }
}

public class AdminProfileResponse
{
    public long AdminId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public List<string> Permissions { get; set; } = new();

    public bool TwoFactorEnabled { get; set; }

    public DateTime? LastLoginAt { get; set; }
}

public class ChangeAdminPasswordRequest
{
    public string OldPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>管理员重置指定账号密码（需管理员权限，不需要旧密码）。</summary>
public class ResetAdminPasswordRequest
{
    /// <summary>新密码（客户端 SHA256 hex）。</summary>
    public string NewPassword { get; set; } = string.Empty;
}

// ============ 验证码 ============

public class CaptchaResponse
{
    /// <summary>验证码 ID（登录时回传）。</summary>
    public string CaptchaId { get; set; } = string.Empty;

    /// <summary>SVG 图片（data:image/svg+xml;base64,...）。</summary>
    public string Image { get; set; } = string.Empty;
}

// ============ 双因素（TOTP） ============

public class TwoFactorSetupResponse
{
    public string Secret { get; set; } = string.Empty;

    public string OtpUri { get; set; } = string.Empty;
}

public class EnableTwoFactorRequest
{
    public string Secret { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}

public class DisableTwoFactorRequest
{
    public string Code { get; set; } = string.Empty;
}

// ============ 角色 ============

public class AdminRoleInfoDto
{
    public string Role { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<string> Permissions { get; set; } = new();
}

public class UpdateAdminRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

// ============ 仪表盘 ============

public class DashboardSummaryResponse
{
    public long TotalPlayers { get; set; }

    public long TodayNewPlayers { get; set; }

    public long OnlinePlayers { get; set; }

    public long TodayActivePlayers { get; set; }

    public long TotalShell { get; set; }

    public long TotalCoupon { get; set; }

    /// <summary>近 7 日注册趋势（按天）。</summary>
    public List<DailyCountDto> RegistrationTrend { get; set; } = new();
}

public class DailyCountDto
{
    public string Date { get; set; } = string.Empty;

    public long Count { get; set; }
}

// ============ 服务器监控 ============

public class ServerOverviewResponse
{
    public string HostName { get; set; } = string.Empty;

    public string DockerVersion { get; set; } = string.Empty;

    public double LoadAvg1 { get; set; }

    public double LoadAvg5 { get; set; }

    public double LoadAvg15 { get; set; }

    /// <summary>宿主机内存（字节）。</summary>
    public long MemTotal { get; set; }

    /// <summary>宿主机已用内存（字节）。</summary>
    public long MemUsed { get; set; }

    /// <summary>宿主机内存使用率（0~1）。</summary>
    public double MemUsagePercent { get; set; }

    /// <summary>宿主机磁盘已用（字节，Docker 根目录所在挂载点）。</summary>
    public long DiskUsed { get; set; }

    /// <summary>宿主机磁盘总量（字节）。</summary>
    public long DiskTotal { get; set; }

    /// <summary>宿主机磁盘使用率（0~1）。</summary>
    public double DiskUsagePercent { get; set; }

    public string DiskMountPoint { get; set; } = string.Empty;

    public List<ContainerInfoDto> Containers { get; set; } = new();

    /// <summary>镜像/容器等 Docker 占用（docker system df 摘要）。</summary>
    public string DockerDiskSummary { get; set; } = string.Empty;
}

public class ContainerInfoDto
{
    public string ContainerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    /// <summary>running / exited / restarting / created / paused / dead。</summary>
    public string State { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public double? CpuPercent { get; set; }

    public long? MemUsage { get; set; }

    public long? MemLimit { get; set; }

    public string Ports { get; set; } = string.Empty;

    public string RunningFor { get; set; } = string.Empty;
}

public class ContainerLogsResponse
{
    public string ContainerName { get; set; } = string.Empty;

    public string Log { get; set; } = string.Empty;
}

// ============ 玩家管理 ============

public class PlayerAdminDto
{
    public long UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public int Level { get; set; }

    public int Shell { get; set; }

    public int Coupon { get; set; }

    public int Exp { get; set; }

    public bool IsBanned { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLogin { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();

    public long Total { get; set; }
}

public class PlayerDetailDto
{
    public PlayerAdminDto Player { get; set; } = new();

    /// <summary>宠物列表。</summary>
    public List<PetAdminDto> Pets { get; set; } = new();

    /// <summary>背包（count &gt; 0）。</summary>
    public List<InventoryItemDto> Inventory { get; set; } = new();

    public int FriendCount { get; set; }
}

public class PetAdminDto
{
    public long PetId { get; set; }

    public string Type { get; set; } = string.Empty;

    public int Level { get; set; }

    public int Exp { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class UpdatePlayerCurrencyRequest
{
    public int? Shell { get; set; }

    public int? Coupon { get; set; }

    public int? Level { get; set; }

    public int? Exp { get; set; }
}

/// <summary>修改玩家昵称/头像等基础资料（字段为 null 表示不修改）。</summary>
public class UpdatePlayerProfileRequest
{
    public string? Nickname { get; set; }

    public string? AvatarUrl { get; set; }
}

public class AdminPlayerSearchRequest
{
    public string? Keyword { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

// ============ 审核 ============

public class ReviewQueueItemDto
{
    public long LetterId { get; set; }

    public long FromUser { get; set; }

    public string FromNickname { get; set; } = string.Empty;

    public long ToUser { get; set; }

    public string ToNickname { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? ItemId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class ReviewLetterRequest
{
    public long LetterId { get; set; }

    /// <summary>true 通过 / false 拒绝。</summary>
    public bool Approve { get; set; }
}

public class BanRequest
{
    public string? Reason { get; set; }
}

// ============ 公告 ============

public class AnnouncementDto
{
    public long AnnouncementId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string CreatorName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }
}

public class AnnouncementRequest
{
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string Status { get; set; } = "draft";
}

// ============ 官方信件 ============

/// <summary>官方发信请求：直接送达玩家信箱，可携带附件（庭贝/邮券/道具/非遗）。</summary>
public class OfficialLetterRequest
{
    /// <summary>收件玩家 ID。</summary>
    public long ToUserId { get; set; }

    /// <summary>信件正文。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>附件类型：shell（庭贝）/ coupon（邮券）/ item（道具/非遗）；空 = 无附件。</summary>
    public string? RewardType { get; set; }

    /// <summary>附件数量（shell/coupon 为货币数额，item 为背包数量）。</summary>
    public int RewardCount { get; set; }

    /// <summary>道具/非遗 ID（reward_type=item 时必填）。</summary>
    public string? ItemId { get; set; }
}

// ============ 数据库 ============

public class ExecuteSqlRequest
{
    /// <summary>直接提交的 SQL（与 SqlFile 二选一）。</summary>
    public string? Sql { get; set; }

    /// <summary>上传的 .sql 文件内容。</summary>
    public string? SqlFile { get; set; }
}

public class ExecuteSqlResultDto
{
    public int StatementCount { get; set; }

    public long AffectedRows { get; set; }

    public double DurationMs { get; set; }

    public string Message { get; set; } = string.Empty;
}

// ============ 数据库可视化 ============

public class DatabaseInfoDto
{
    public string Name { get; set; } = string.Empty;
}

public class TableInfoDto
{
    public string Name { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;

    public long Rows { get; set; }
}

public class ColumnInfoDto
{
    public string Name { get; set; } = string.Empty;

    public string ColumnType { get; set; } = string.Empty;

    public bool Nullable { get; set; }

    public bool IsPrimaryKey { get; set; }

    public string? DefaultValue { get; set; }

    public string Comment { get; set; } = string.Empty;
}

public class TableDataResultDto
{
    public List<ColumnInfoDto> Columns { get; set; } = new();

    public List<Dictionary<string, object?>> Rows { get; set; } = new();

    public long Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

public class RowChangeRequest
{
    /// <summary>数据库名；为空时使用连接串默认库。</summary>
    public string? Database { get; set; }

    public string Table { get; set; } = string.Empty;

    /// <summary>变更的列值；值为 null 表示 SQL NULL。</summary>
    public Dictionary<string, object?> Values { get; set; } = new();

    /// <summary>主键列名（更新/删除时定位行）；为空时服务端自动识别表主键。</summary>
    public string? PrimaryKey { get; set; }

    /// <summary>主键值。</summary>
    public object? PrimaryKeyValue { get; set; }
}

/// <summary>结构化行过滤条件（参数化绑定，防 SQL 注入）。</summary>
public class RowFilterDto
{
    /// <summary>列名（必须为表中真实列）。</summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>运算符：= != &gt; &gt;= &lt; &lt;= like in。</summary>
    public string Op { get; set; } = "=";

    /// <summary>比较值；in 运算符时为数组。</summary>
    public object? Value { get; set; }
}

public class TableSchemaResultDto
{
    public string Database { get; set; } = string.Empty;

    public string Table { get; set; } = string.Empty;

    public List<ColumnInfoDto> Columns { get; set; } = new();

    public List<IndexInfoDto> Indexes { get; set; } = new();
}

public class IndexInfoDto
{
    public string Name { get; set; } = string.Empty;

    public bool Unique { get; set; }

    public bool IsPrimary { get; set; }

    public string Columns { get; set; } = string.Empty;
}

public class QueryRequest
{
    public string Sql { get; set; } = string.Empty;
}

public class QueryResultDto
{
    /// <summary>是否查询语句（SELECT）。</summary>
    public bool IsQuery { get; set; }

    public List<string> Columns { get; set; } = new();

    public List<Dictionary<string, object?>> Rows { get; set; } = new();

    public long Total { get; set; }

    public string Message { get; set; } = string.Empty;
}

// ============ 部署 ============

public class DeployStatusResponse
{
    public bool Enabled { get; set; }

    public string HostCodeRoot { get; set; } = string.Empty;

    public string ComposeFile { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string Status { get; set; } = "idle";

    public DateTime? LastDeployAt { get; set; }

    public string LastDeployResult { get; set; } = string.Empty;

    public bool Building { get; set; }
}

public class DeployBuildRequest
{
    /// <summary>可选：服务名。空 = 全部（API 蓝绿切换 + 重建 nginx 前端）；api/api-blue/api-green = 仅 API 蓝绿切换；其他服务名 = 直接重建该服务。</summary>
    public string? Service { get; set; }

    /// <summary>是否强制重建镜像（--build / --no-cache）。</summary>
    public bool NoCache { get; set; }
}

public class GitStatusResponse
{
    /// <summary>是否为 git 仓库（向上找到 .git 目录）。</summary>
    public bool GitRepo { get; set; }

    /// <summary>当前分支。</summary>
    public string? Branch { get; set; }

    /// <summary>当前 commit 完整哈希。</summary>
    public string? Commit { get; set; }

    /// <summary>当前 commit 短哈希（8 位）。</summary>
    public string? CommitShort { get; set; }

    /// <summary>当前 commit 提交信息首行。</summary>
    public string? CommitMessage { get; set; }

    /// <summary>领先远端提交数（本地有而未推送）。</summary>
    public int Ahead { get; set; }

    /// <summary>落后远端提交数（远端有新而本地未拉取）。</summary>
    public int Behind { get; set; }

    /// <summary>工作区是否有未提交修改（在线编辑/上传覆盖会导致，会阻塞 git pull）。</summary>
    public bool Dirty { get; set; }

    /// <summary>未提交修改的文件列表（相对仓库根）。</summary>
    public List<string> DirtyFiles { get; set; } = new();

    /// <summary>获取 git 状态失败时的错误信息（非仓库/未装 git/权限等）。</summary>
    public string? Error { get; set; }
}

public class GitPullResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>pull 后的最新 commit 短哈希。</summary>
    public string? CommitShort { get; set; }
}

// ============ 部署：文件管理 ============

public class FileEntryDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>相对代码根目录的路径（目录/文件），目录统一不带结尾斜杠。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>dir / file。</summary>
    public string Type { get; set; } = string.Empty;

    public long Size { get; set; }

    public string ModifiedAt { get; set; } = string.Empty;
}

public class CreateDirectoryRequest
{
    /// <summary>相对代码根目录的新目录路径，如 src/WildCourt.Api/Services。</summary>
    public string Path { get; set; } = string.Empty;
}

public class FileContentDto
{
    /// <summary>相对代码根目录的文件路径。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>文件文本内容（BOM 已剥离）。</summary>
    public string Content { get; set; } = string.Empty;

    public long Size { get; set; }

    /// <summary>utf-8 / utf-8-bom，保存时原样回传以保留原始编码。</summary>
    public string Encoding { get; set; } = "utf-8";

    public string ModifiedAt { get; set; } = string.Empty;
}

public class SaveFileRequest
{
    /// <summary>相对代码根目录的文件路径。</summary>
    public string Path { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>文件编码（utf-8 / utf-8-bom），由读取接口返回后原样回传。</summary>
    public string Encoding { get; set; } = "utf-8";
}

// ============ 管理员账号 ============

public class AdminItemDto
{
    public long AdminId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
}

public class CreateAdminRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>角色：superadmin / operator / support / readonly，默认 operator。</summary>
    public string Role { get; set; } = AdminRoleDefaults.DefaultRole;
}

// ============ 操作审计日志 ============

public class AdminOperationLogDto
{
    public long LogId { get; set; }

    public long? AdminId { get; set; }

    public string AdminName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public int StatusCode { get; set; }

    public long DurationMs { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class AuditLogQueryRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    /// <summary>按管理员 ID 筛选。</summary>
    public long? AdminId { get; set; }

    /// <summary>按操作名精确匹配，如 player.update-currency。</summary>
    public string? Action { get; set; }

    /// <summary>关键词：匹配操作名 / 路径 / 管理员名。</summary>
    public string? Keyword { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int? StatusCode { get; set; }
}
