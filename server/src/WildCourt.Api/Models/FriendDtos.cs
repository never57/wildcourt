namespace WildCourt.Api.Models;

/// <summary>偷菜请求（POST /api/v1/friend/steal）。</summary>
public class StealCropRequest
{
    public long TargetUserId { get; set; }

    /// <summary>目标农田格（0 起）。</summary>
    public int CellId { get; set; }
}

/// <summary>偷菜结果（count = floor(yield × stealRatio)，受余量限制）。</summary>
public class StealResultDto
{
    public long TargetUserId { get; set; }

    public string CropId { get; set; } = string.Empty;

    public int Count { get; set; }
}
