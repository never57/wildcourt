namespace WlidCourt.Api.Models;

/// <summary>
/// 庭院模型（对应 yards 表）。
/// </summary>
public class Yard
{
    public long UserId { get; set; }

    public string LayoutJson { get; set; } = string.Empty;

    public int Level { get; set; }

    public DateTime UpdatedAt { get; set; }
}
