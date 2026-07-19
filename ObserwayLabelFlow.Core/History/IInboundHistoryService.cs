namespace ObserwayLabelFlow.Core.History;

public sealed class InboundHistoryFilter
{
    /// <summary>Inclusive local calendar day (converted to UTC range by the service).</summary>
    public DateOnly DayLocal { get; set; }

    public string? SearchText { get; set; }

    public int Take { get; set; } = 1000;
}

public interface IInboundHistoryService
{
    Task AddAsync(InboundHistoryEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<InboundHistoryEntry>> GetForDayAsync(InboundHistoryFilter filter, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);
}
