namespace WildCourt.Data.Entities;

/// <summary>
/// 宠物表（表名 pets）。
/// 对应文档 15.3：pet_id, user_id, type, level, exp, status。
/// 状态机：在家 → 派遣中 → 休息中 → 在家（见文档 02 §2.3 PetService）。
/// </summary>
public class PetEntity
{
    public long PetId { get; set; }

    public long UserId { get; set; }

    public string Type { get; set; } = string.Empty;

    public int Level { get; set; }

    public int Exp { get; set; }

    /// <summary>在家 / 派遣中 / 休息中</summary>
    public string Status { get; set; } = string.Empty;
}
