namespace WildCourt.Api.Models;

/// <summary>
/// 偷菜机制配置行（Sheet14 StealMechanics / Config_StealMechanics.json，文档 08 §8.3、16 §16.2）。
/// </summary>
public class StealMechanicsConfigRow
{
    /// <summary>偷取比例（0.3）。</summary>
    public double StealRatio { get; set; }

    /// <summary>每好友每作物每天可偷次数（1）。</summary>
    public int DailyLimitPerCrop { get; set; }

    /// <summary>偷者每天总次数（20）。</summary>
    public int DailyLimitTotal { get; set; }

    /// <summary>仅成熟可偷（true）。</summary>
    public bool MatureOnly { get; set; }

    /// <summary>保护罩持续时间（小时，24）。</summary>
    public int ProtectionDuration { get; set; }
}
