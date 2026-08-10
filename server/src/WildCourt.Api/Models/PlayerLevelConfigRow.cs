namespace WildCourt.Api.Models;

/// <summary>
/// 玩家等级配置行（Sheet17 PlayerLevel / Config_PlayerLevel.json，文档 16 §16.2）。
/// </summary>
public class PlayerLevelConfigRow
{
    public int Level { get; set; }

    /// <summary>升到该等级所需累计经验。</summary>
    public int TotalExp { get; set; }

    /// <summary>对应庭院解锁进度等级（1~4，见 Sheet16 YardUnlock）。</summary>
    public int YardProgress { get; set; }

    public string Note { get; set; } = string.Empty;
}
