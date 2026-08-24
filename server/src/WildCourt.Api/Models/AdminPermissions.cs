namespace WildCourt.Api.Models;

/// <summary>
/// RBAC 权限点定义与角色映射。
/// 权限点格式：模块:操作。各控制器通过 AdminControllerBase.RequirePermission 校验。
/// </summary>
public static class AdminPermissions
{
    // ============ 权限点常量 ============

    public const string DashboardView = "dashboard:view";

    public const string ServerView = "server:view";
    public const string ServerRestart = "server:restart";

    public const string PlayersView = "players:view";
    public const string PlayersEdit = "players:edit";     // 修改资料/货币/等级
    public const string PlayersBan = "players:ban";       // 封禁/解封

    public const string ReviewsView = "reviews:view";
    public const string ReviewsEdit = "reviews:edit";     // 信件审核操作

    public const string AnnouncementsView = "announcements:view";
    public const string AnnouncementsEdit = "announcements:edit";

    public const string ReportsView = "reports:view";   // 查看举报审理
    public const string ReportsEdit = "reports:edit";   // 处理举报/封禁聊天

    public const string DatabaseView = "database:view";        // 查看表结构/数据
    public const string DatabaseExecute = "database:execute";  // 执行 SQL（含写操作）

    public const string DeployView = "deploy:view";
    public const string DeployEdit = "deploy:edit";   // 上传/删除/保存/构建/执行命令

    public const string AuditView = "audit:view";

    public const string AdminsView = "admins:view";
    public const string AdminsEdit = "admins:edit";

    // ============ 角色定义 ============

    public const string RoleSuperAdmin = "superadmin";
    public const string RoleOperator = "operator";
    public const string RoleSupport = "support";
    public const string RoleReadonly = "readonly";

    public static readonly string[] AllRoles =
    {
        RoleSuperAdmin,
        RoleOperator,
        RoleSupport,
        RoleReadonly,
    };

    /// <summary>角色 → 权限集合。</summary>
    public static readonly IReadOnlyDictionary<string, HashSet<string>> RolePermissions =
        new Dictionary<string, HashSet<string>>
        {
            [RoleSuperAdmin] = new HashSet<string>
            {
                DashboardView, ServerView, ServerRestart,
                PlayersView, PlayersEdit, PlayersBan,
                ReviewsView, ReviewsEdit,
                ReportsView, ReportsEdit,
                AnnouncementsView, AnnouncementsEdit,
                DatabaseView, DatabaseExecute,
                DeployView, DeployEdit,
                AuditView,
                AdminsView, AdminsEdit,
            },
            [RoleOperator] = new HashSet<string>
            {
                DashboardView, ServerView,
                PlayersView, PlayersEdit, PlayersBan,
                ReviewsView, ReviewsEdit,
                ReportsView, ReportsEdit,
                AnnouncementsView, AnnouncementsEdit,
                DatabaseView,
                DeployView,
                AuditView,
            },
            [RoleSupport] = new HashSet<string>
            {
                DashboardView, ServerView,
                PlayersView,
                ReviewsView, ReviewsEdit,
                ReportsView, ReportsEdit,
                AnnouncementsView,
                AuditView,
            },
            [RoleReadonly] = new HashSet<string>
            {
                DashboardView, ServerView,
                PlayersView,
                ReviewsView,
                ReportsView,
                AnnouncementsView,
                AuditView,
            },
        };

    /// <summary>角色显示名。</summary>
    public static readonly IReadOnlyDictionary<string, string> RoleNames =
        new Dictionary<string, string>
        {
            [RoleSuperAdmin] = "超级管理员",
            [RoleOperator] = "运营",
            [RoleSupport] = "客服",
            [RoleReadonly] = "只读",
        };

    public static string GetRoleName(string role) =>
        RoleNames.TryGetValue(role, out var name) ? name : role;

    /// <summary>校验角色值是否合法；非法时抛异常。</summary>
    public static void EnsureValidRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role) || !RolePermissions.ContainsKey(role))
        {
            throw new BusinessException(ApiCodes.BadRequest,
                $"角色不合法，可选值：{string.Join("/", AllRoles)}");
        }
    }

    /// <summary>获取角色拥有的权限集合。</summary>
    public static HashSet<string> GetPermissions(string role) =>
        RolePermissions.TryGetValue(role, out var perms) ? perms : new HashSet<string>();

    /// <summary>是否为超管角色。</summary>
    public static bool IsSuperAdmin(string role) => role == RoleSuperAdmin;
}
