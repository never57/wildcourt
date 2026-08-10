namespace WildCourt.Api.Models;

/// <summary>
/// 宠物信息模型（对应 pets 表）。
/// </summary>
public class Pet
{
    public long PetId { get; set; }

    public long UserId { get; set; }

    public string Type { get; set; } = string.Empty;

    public int Level { get; set; }

    public int Exp { get; set; }

    public string Status { get; set; } = string.Empty;
}
