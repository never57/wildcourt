namespace WlidCourt.Api.Models;

/// <summary>背包条目（GET /api/v1/user/inventory，阶段 C 编辑面板“已拥有”状态）。</summary>
public class InventoryItemDto
{
    public string ItemId { get; set; } = string.Empty;

    public int Count { get; set; }
}

/// <summary>背包响应。</summary>
public class InventoryResponse
{
    public List<InventoryItemDto> Items { get; set; } = new();
}
