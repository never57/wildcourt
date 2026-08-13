namespace WildCourt.Net.Protocol
{
    /// <summary>REST 接口路径（文档 02 §2.2，与 Controllers 对齐）。</summary>
    public static class ApiPaths
    {
        public const string Base = "/api/v1";

        // UserController
        public const string UserRegister = Base + "/user/register";
        public const string UserLogin = Base + "/user/login";
        public const string UserProfile = Base + "/user/profile";
        public const string UserLevel = Base + "/user/level";
        public const string UserExp = Base + "/user/exp";
        public const string UserInventory = Base + "/user/inventory";

        // PetController
        public const string PetInfo = Base + "/pet/info";
        public const string PetDispatch = Base + "/pet/dispatch";
        public const string PetRecall = Base + "/pet/recall";

        // LetterController（仅官方信息发放：收件箱 → 领取附件 → 删除已读）
        public const string LetterInbox = Base + "/letter/inbox";
        public const string LetterClaim = Base + "/letter/claim";
        public static string LetterDelete(long letterId) => Base + $"/letter/{letterId}";

        // FriendController
        public const string FriendApply = Base + "/friend/apply";
        public const string FriendAccept = Base + "/friend/accept";
        public const string FriendSteal = Base + "/friend/steal";
        public const string FriendFeed = Base + "/friend/feed";
        public static string FriendYard(long userId) => Base + $"/friend/yard/{userId}";

        // ShopController
        public const string ShopShelves = Base + "/shop/shelves";
        public const string ShopBuy = Base + "/shop/buy";
        public const string ShopSell = Base + "/shop/sell";
        public const string ShopBaitShelf = Base + "/shop/bait-shelf";

        // YardController
        public const string YardGet = Base + "/yard";
        public const string YardLayout = Base + "/yard/layout";
        public const string YardPlant = Base + "/yard/plant";
        public const string YardPlots = Base + "/yard/plots";
        public const string YardHarvest = Base + "/yard/harvest";
        public static string YardVisit(long userId) => Base + $"/yard/visit/{userId}";

        // ActivityController
        public const string ActivityList = Base + "/activity/list";
        public static string ActivityClaim(string activityId) => Base + $"/activity/claim/{activityId}";

        // AnnouncementController（公开接口，免登录）
        public const string Announcements = Base + "/announcements";

        // DailySignInController
        public const string SignInToday = Base + "/signin/today";
        public const string SignInClaimWeek = Base + "/signin/claim-week";
        public const string SignInClaimMonth = Base + "/signin/claim-month";

        // MiniGameController
        public static string MiniGameStart(string gameId) => Base + $"/minigame/start/{gameId}";
        public const string MiniGameFinish = Base + "/minigame/finish";
        public const string MiniGameDailyCap = Base + "/minigame/daily-cap";
    }

    /// <summary>统一业务码（0 成功，非 0 错误；与服务端 ExceptionMiddleware 对齐）。</summary>
    public static class ApiCodes
    {
        public const int Success = 0;
        public const int BadRequest = 400;
        public const int Unauthorized = 401;    // 未登录/token 失效
        public const int Forbidden = 403;
        public const int NotFound = 404;
        public const int Conflict = 409;    // 状态冲突（如账号已存在）
        public const int ServerError = 500;
        public const int NetworkError = -1;     // 客户端本地：网络不通
        public const int Timeout = -2;          // 客户端本地：请求超时
    }

    /// <summary>
    /// SignalR 推送事件类型（文档 02 §2.5 ChatHub、03 §3.6）：
    /// 实时事件经 Net/Handler 按类型路由到对应模块。
    /// </summary>
    public enum NetEvent
    {
        /// <summary>新信件送达（含随附非遗入信箱）。</summary>
        LetterArrived,

        /// <summary>收到好友申请。</summary>
        FriendApply,

        /// <summary>通用通知（访问留下/空盆离开等）。</summary>
        Notice,

        /// <summary>每日结算播报（04:00，文档 14 §14.5）。</summary>
        DailySettlement,

        /// <summary>徽章解锁。</summary>
        BadgeUnlocked,

        /// <summary>货币变化（庭贝/邮券）。</summary>
        CurrencyChanged,

        /// <summary>活动激活/关闭。</summary>
        ActivityChanged,
    }
}
