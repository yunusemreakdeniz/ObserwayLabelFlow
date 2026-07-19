using Microsoft.EntityFrameworkCore;
using ObserwayLabelFlow.App.Data;
using ObserwayLabelFlow.Core.History;

namespace ObserwayLabelFlow.App.Services;

public sealed class InboundHistoryService(IDbContextFactory<LabelFlowDbContext> factory) : IInboundHistoryService
{
    public async Task AddAsync(InboundHistoryEntry entry, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.InboundHistory.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<InboundHistoryEntry>> GetForDayAsync(InboundHistoryFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Take = Math.Clamp(filter.Take, 1, 5000);

        var (fromUtc, toUtcExclusive) = GetUtcRangeForLocalDay(filter.DayLocal);

        await using var db = await factory.CreateDbContextAsync(ct);

        // SQLite cannot translate DateTimeOffset range comparisons; filter in memory.
        // Id is autoincrement so newest rows have the largest Id (ORDER BY DateTimeOffset is also unreliable).
        var rows = await db.InboundHistory.AsNoTracking().ToListAsync(ct);

        IEnumerable<InboundHistoryEntry> filtered = rows
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtcExclusive);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var term = filter.SearchText.Trim();
            filtered = filtered.Where(x =>
                ContainsIgnoreCase(x.Reference, term) ||
                ContainsIgnoreCase(x.OrderNumber, term) ||
                ContainsIgnoreCase(x.MarkedBy, term) ||
                ContainsIgnoreCase(x.ErrorMessage, term));
        }

        return filtered
            .OrderByDescending(x => x.Id)
            .Take(filter.Take)
            .ToList();
    }

    private static bool ContainsIgnoreCase(string? value, string term)
        => !string.IsNullOrEmpty(value)
           && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.InboundHistory.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        if (rows == 0)
            throw new InvalidOperationException($"Inbound history entry not found: {id}");
    }

    private static (DateTimeOffset FromUtc, DateTimeOffset ToUtcExclusive) GetUtcRangeForLocalDay(DateOnly dayLocal)
    {
        var tz = TimeZoneInfo.Local;
        var startLocal = DateTime.SpecifyKind(dayLocal.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(dayLocal.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
        return (new DateTimeOffset(startUtc), new DateTimeOffset(endUtc));
    }
}
