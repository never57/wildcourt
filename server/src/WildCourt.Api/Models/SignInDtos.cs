namespace WildCourt.Api.Models;

/// <summary>今日签到状态（GET /api/v1/signin/today，对齐客户端 SignInTodayResponse）。</summary>
public class SignInTodayResponse
{
    /// <summary>今日是否已签到。</summary>
    public bool SignedToday { get; set; }

    /// <summary>连续签到天数（今日未签则截至昨日）。</summary>
    public int Days { get; set; }
}

/// <summary>签到领取结果（POST /api/v1/signin/claim-week | claim-month）。</summary>
public class SignInClaimResponse
{
    /// <summary>本次发放的庭贝。</summary>
    public int Shell { get; set; }

    /// <summary>本次发放的道具（空表示无）。</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>道具数量。</summary>
    public int ItemCount { get; set; }

    /// <summary>领取后累计签到天数（月签到用）。</summary>
    public int TotalDays { get; set; }
}
