namespace ObserwayLabelFlow.Core.History;

public sealed class HistoryFilter
{
    public string? SearchText { get; set; }

    public DateTimeOffset? FromDateUtc { get; set; }

    public DateTimeOffset? ToDateUtc { get; set; }

    public bool OnlyErrors { get; set; }

    public int Take { get; set; } = 200;
}

public interface IHistoryService
{
    Task AddAsync(PrintHistoryEntry entry, CancellationToken ct = default);

    Task UpdateAsync(PrintHistoryEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<PrintHistoryEntry>> GetRecentAsync(int take = 200, CancellationToken ct = default);

    Task<IReadOnlyList<PrintHistoryEntry>> GetAsync(HistoryFilter filter, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);

    Task DeleteManyAsync(IEnumerable<long> ids, CancellationToken ct = default);
}
