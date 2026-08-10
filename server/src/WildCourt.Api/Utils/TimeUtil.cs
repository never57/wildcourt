namespace WildCourt.Api.Utils;

/// <summary>
/// 时间工具：UTC+8 时间、04:00 结算边界计算、跨天判断（文档 02 §2.7）。
/// </summary>
public static class TimeUtil
{
    /// <summary>当前 UTC+8 时间</summary>
    public static DateTime NowUtc8()
    {
        throw new NotImplementedException();
    }

    /// <summary>最近一次 04:00（UTC+8）结算边界</summary>
    public static DateTime LastSettlementBoundary(DateTime now)
    {
        throw new NotImplementedException();
    }

    /// <summary>是否跨天（按 04:00 结算边界）</summary>
    public static bool IsNewSettlementDay(DateTime last, DateTime now)
    {
        throw new NotImplementedException();
    }
}
