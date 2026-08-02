namespace ObserwayLabelFlow.Core.Warehouse;

public sealed class WarehouseDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool ShowProductDetailsOnInbound { get; set; }
    public bool AllowInbound { get; set; }
    public bool AllowOutbound { get; set; }
}
