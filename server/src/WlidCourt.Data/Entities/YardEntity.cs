namespace WlidCourt.Data.Entities;

/// <summary>
/// 庭院表（表名 yards）。
/// 对应文档 15.3：user_id, layout_json, level, updated_at。
/// </summary>
public class YardEntity
{
    public long UserId { get; set; }

    /// <summary>布局 JSON（3 个布局槽，见文档 02 §2.2 YardController）</summary>
    public string LayoutJson { get; set; } = string.Empty;

    public int Level { get; set; }

    public DateTime UpdatedAt { get; set; }
}
