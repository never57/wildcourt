using System;

namespace WildCourt.Utils
{
    /// <summary>
    /// 时间工具（文档 17 §1.7）：统一服务器时区 UTC+8；每日结算固定 04:00:00 服务器时间。
    /// 服务端 TimeUtil 同规则实现（文档 02 §2.7）。
    /// </summary>
    public static class TimeUtil
    {
        /// <summary>服务器时区偏移（UTC+8）。</summary>
        public static readonly TimeSpan ServerOffset = TimeSpan.FromHours(8);

        /// <summary>每日结算时刻（服务器时间 04:00）。</summary>
        public static readonly TimeSpan SettlementTime = new TimeSpan(4, 0, 0);

        /// <summary>取当前服务器时间（DateTime.Now + UTC+8，按本机时区近似；正式联调以服务端下发为准）。</summary>
        public static DateTime NowServerTime()
        {
            return DateTime.UtcNow + ServerOffset;
        }

        /// <summary>时间戳（秒，UTC+8 基准，与 Unix 时间戳一致，仅作排序/缓存键）。</summary>
        public static long NowUnixSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>判断两个日期是否为同一天（按服务器时区）。</summary>
        public static bool IsSameDay(DateTime a, DateTime b)
        {
            var sa = ToServerDate(a);
            var sb = ToServerDate(b);
            return sa.Year == sb.Year && sa.Month == sb.Month && sa.Day == sb.Day;
        }

        /// <summary>转换为服务器时区日期（去掉时分秒）。</summary>
        public static DateTime ToServerDate(DateTime time)
        {
            return time.Date;
        }

        /// <summary>最近一次结算时刻（04:00）。若当前未到今日 04:00，返回昨日 04:00。</summary>
        public static DateTime LastSettlementTime(DateTime serverNow)
        {
            var today = serverNow.Date;
            var todaySettle = today + SettlementTime;
            return serverNow >= todaySettle ? todaySettle : todaySettle - TimeSpan.FromDays(1);
        }

        /// <summary>服务器时间字符串（yyyy-MM-dd HH:mm:ss，用于展示与活动配置 startTime/endTime 对比）。</summary>
        public static string ToServerString(DateTime time)
        {
            return time.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
