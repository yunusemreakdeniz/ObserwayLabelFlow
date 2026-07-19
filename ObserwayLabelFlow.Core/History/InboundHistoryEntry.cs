namespace ObserwayLabelFlow.Core.History;

public sealed class InboundHistoryEntry
{
    public long Id { get; set; }

    /// <summary>Scanned or typed reference (OBS or purchase tracking).</summary>
    public string Reference { get; set; } = string.Empty;

    public string? OrderNumber { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public string? MarkedBy { get; set; }
}
