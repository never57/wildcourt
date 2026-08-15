namespace WildCourt.Core
{
    /// <summary>
    /// 全局事件类型集合。各系统可扩展自己的事件结构体，
    /// 经 <see cref="EventBus"/> 发布/订阅（均为值类型，避免 GC 压力）。
    /// </summary>

    /// <summary>启动初始化完成，可以进入场景流。</summary>
    public struct GameReadyEvent
    {
    }

    /// <summary>配置表重新加载完成（热更后）。</summary>
    public struct ConfigReloadedEvent
    {
    }

    /// <summary>玩家经验/等级变化（本地预估，服务端 PlayerService 为权威）。</summary>
    public struct PlayerExpChangedEvent
    {
        public int Level;
        public int Exp;
    }

    /// <summary>HTTP 网络连接状态。</summary>
    public struct NetConnectedEvent
    {
    }

    /// <summary>HTTP 网络连接断开。</summary>
    public struct NetDisconnectedEvent
    {
        public string Reason;
    }

    /// <summary>SignalR 实时连接建立（私聊/通知/结算播报可用）。</summary>
    public struct RealtimeConnectedEvent
    {
    }

    /// <summary>实时连接断开（断线重连中）。</summary>
    public struct RealtimeDisconnectedEvent
    {
        public string Reason;
    }

    /// <summary>服务端通用通知推送（SignalR）。</summary>
    public struct ServerNoticeEvent
    {
        public string Content;
    }

    /// <summary>新信件送达（SignalR 推送，文档 07 §7.10）。</summary>
    public struct LetterArrivedEvent
    {
        public long LetterId;
    }

    /// <summary>收到好友申请（SignalR 推送）。</summary>
    public struct FriendApplyEvent
    {
        public long FromUserId;
    }

    /// <summary>收到好友私聊消息（ChatHub ReceivePrivateMessage 推送）。</summary>
    public struct PrivateMessageEvent
    {
        public long Id;
        public long FromUserId;
        public long ToUserId;
        public string Content;
        public long CreatedAt;
    }

    /// <summary>每日结算播报（04:00 推送，文档 14 §14.5）。</summary>
    public struct DailySettlementEvent
    {
        public string Report;
    }

    /// <summary>货币变化（庭贝 shell / 邮券 coupon）。</summary>
    public struct CurrencyChangedEvent
    {
        public long Shell;
        public long Coupon;
    }

    /// <summary>玩家资料更新（昵称/头像等，客户端本地改动后广播，供各处头像/昵称同步）。</summary>
    public struct ProfileUpdatedEvent
    {
        public string AvatarUrl;
        public string Nickname;
    }

    /// <summary>徽章解锁。</summary>
    public struct BadgeUnlockedEvent
    {
        public string BadgeId;
    }

    /// <summary>活动激活/关闭状态变化（Lua 配置驱动，文档 13）。</summary>
    public struct ActivityChangedEvent
    {
        public string ActivityId;
        public bool Active;
    }
}
