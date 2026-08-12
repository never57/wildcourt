namespace WildCourt.Api.Models;

/// <summary>
/// 签到配置行（Config_DailySignIn.json，对齐客户端 Config_DailySignIn.lua，文档 11 §11.6）。
/// kind = "week" 时按 day（1~7 循环）；kind = "month" 时按 total（累计签到天数里程碑）。
/// </summary>
public class SignInConfigRow
{
    /// <summary>行类型：week（周签到）/ month（月签到）。</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>周签到第几天（1~7，循环）。</summary>
    public int Day { get; set; }

    /// <summary>月签到里程碑：累计签到满 total 天可领。</summary>
    public int Total { get; set; }

    /// <summary>庭贝奖励。</summary>
    public int Shell { get; set; }

    /// <summary>道具奖励（空表示无）。</summary>
    public string Item { get; set; } = string.Empty;

    /// <summary>道具数量。</summary>
    public int Count { get; set; }
}
