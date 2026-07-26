namespace ObserwayLabelFlow.Core.History;

public sealed class HistoryFilter
{
    /// <summary>Inclusive local calendar day (converted to UTC range by the service).</summary>
    public DateOnly DayLocal { get; set; }

    public string? SearchText { get; set; }

    public bool OnlyErrors { get; set; }

    public int Take { get; set; } = 2000;
}

public interface IHistoryService
{
    Task AddAsync(PrintHistoryEntry entry, CancellationToken ct = default);

    Task UpdateAsync(PrintHistoryEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<PrintHistoryEntry>> GetForDayAsync(HistoryFilter filter, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);

    Task DeleteManyAsync(IEnumerable<long> ids, CancellationToken ct = default);
}
