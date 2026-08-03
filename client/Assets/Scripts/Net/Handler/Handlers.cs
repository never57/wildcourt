using System;
using WildCourt.Core;
using WildCourt.Net.Protocol;
using WildCourt.Utils;

namespace WildCourt.Net.Handler
{
    /// <summary>新信件送达处理器（文档 07 §7.10：实时收信）。</summary>
    public class LetterHandler : INetEventHandler
    {
        public static readonly LetterHandler Instance = new LetterHandler();

        public void HandleNetEvent(NetEvent evt, string data)
        {
            switch (evt)
            {
                case NetEvent.LetterArrived:
                    // data 形如 { "letterId": 123 }
                    long letterId = 0;
                    var raw = JsonUtil.GetField(data, "letterId");
                    if (raw != null) long.TryParse(raw, out letterId);
                    EventBus.Publish(new LetterArrivedEvent { LetterId = letterId });
                    break;
            }
        }
    }

    /// <summary>好友模块处理器：好友申请 → 好友模块。</summary>
    public class FriendHandler : INetEventHandler
    {
        public static readonly FriendHandler Instance = new FriendHandler();

        public void HandleNetEvent(NetEvent evt, string data)
        {
            switch (evt)
            {
                case NetEvent.FriendApply:
                    long fromUserId = 0;
                    var raw = JsonUtil.GetField(data, "fromUserId");
                    if (raw != null) long.TryParse(raw, out fromUserId);
                    EventBus.Publish(new FriendApplyEvent { FromUserId = fromUserId });
                    break;
            }
        }
    }

    /// <summary>通用通知处理器（访问留下/空盆离开等）。</summary>
    public class NoticeHandler : INetEventHandler
    {
        public static readonly NoticeHandler Instance = new NoticeHandler();

        public void HandleNetEvent(NetEvent evt, string data)
        {
            switch (evt)
            {
                case NetEvent.Notice:
                    EventBus.Publish(new ServerNoticeEvent { Content = data });
                    break;
            }
        }
    }

    /// <summary>每日结算播报处理器（04:00 推送，文档 14 §14.5）。</summary>
    public class SettlementHandler : INetEventHandler
    {
        public static readonly SettlementHandler Instance = new SettlementHandler();

        public void HandleNetEvent(NetEvent evt, string data)
        {
            switch (evt)
            {
                case NetEvent.DailySettlement:
                    EventBus.Publish(new DailySettlementEvent { Report = data });
                    break;
            }
        }
    }

    /// <summary>活动/货币/徽章等状态处理器。</summary>
    public class SystemStateHandler : INetEventHandler
    {
        public static readonly SystemStateHandler Instance = new SystemStateHandler();

        public void HandleNetEvent(NetEvent evt, string data)
        {
            switch (evt)
            {
                case NetEvent.BadgeUnlocked:
                    EventBus.Publish(new BadgeUnlockedEvent { BadgeId = JsonUtil.GetField(data, "badgeId") ?? string.Empty });
                    break;
                case NetEvent.CurrencyChanged:
                    long shell = 0, coupon = 0;
                    var s = JsonUtil.GetField(data, "shell");
                    var c = JsonUtil.GetField(data, "coupon");
                    if (s != null) long.TryParse(s, out shell);
                    if (c != null) long.TryParse(c, out coupon);
                    EventBus.Publish(new CurrencyChangedEvent { Shell = shell, Coupon = coupon });
                    break;
                case NetEvent.ActivityChanged:
                    EventBus.Publish(new ActivityChangedEvent
                    {
                        ActivityId = JsonUtil.GetField(data, "activityId") ?? string.Empty,
                        Active = (JsonUtil.GetField(data, "active") ?? "false") == "true",
                    });
                    break;
            }
        }
    }

    /// <summary>内置处理器注册（在 NetManager.Init 或启动时调用一次）。</summary>
    public static class DefaultHandlers
    {
        public static void RegisterAll()
        {
            var center = HandlerCenter.Instance;
            center.Register(NetEvent.LetterArrived, LetterHandler.Instance);
            center.Register(NetEvent.FriendApply, FriendHandler.Instance);
            center.Register(NetEvent.Notice, NoticeHandler.Instance);
            center.Register(NetEvent.DailySettlement, SettlementHandler.Instance);
            center.Register(NetEvent.BadgeUnlocked, SystemStateHandler.Instance);
            center.Register(NetEvent.CurrencyChanged, SystemStateHandler.Instance);
            center.Register(NetEvent.ActivityChanged, SystemStateHandler.Instance);
        }
    }
}
