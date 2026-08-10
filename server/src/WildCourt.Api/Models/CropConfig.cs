namespace WildCourt.Api.Models;

/// <summary>
/// 作物配置行（Sheet3 Crops / Config_Crops.json，文档 04 §4.2）。
/// 字段与客户端 CropConfig.cs 对齐：id/name/growTime/yield/sellPrice/processTo/processTime。
/// </summary>
public class CropConfigRow
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>生长时间（分钟）。</summary>
    public int GrowTime { get; set; }

    public int Yield { get; set; }

    public int SellPrice { get; set; }

    /// <summary>加工产物 ID（无则为空）。</summary>
    public string ProcessTo { get; set; } = string.Empty;

    /// <summary>加工时间（分钟，无则 0）。</summary>
    public int ProcessTime { get; set; }
}
