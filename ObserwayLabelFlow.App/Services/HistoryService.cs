using Microsoft.EntityFrameworkCore;
using ObserwayLabelFlow.App.Data;
using ObserwayLabelFlow.Core.History;

namespace ObserwayLabelFlow.App.Services;

public sealed class HistoryService(IDbContextFactory<LabelFlowDbContext> factory) : IHistoryService
{
    public async Task AddAsync(PrintHistoryEntry entry, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.PrintHistory.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PrintHistoryEntry entry, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.PrintHistory.Update(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PrintHistoryEntry>> GetForDayAsync(HistoryFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Take = Math.Clamp(filter.Take, 1, 5000);

        var (fromUtc, toUtcExclusive) = GetUtcRangeForLocalDay(filter.DayLocal);

        await using var db = await factory.CreateDbContextAsync(ct);

        // SQLite cannot reliably translate DateTimeOffset range comparisons; filter in memory.
        var rows = await db.PrintHistory.AsNoTracking().ToListAsync(ct);

        IEnumerable<PrintHistoryEntry> filtered = rows
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtcExclusive);

        if (filter.OnlyErrors)
            filtered = filtered.Where(x => !x.Success);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var term = filter.SearchText.Trim();
            filtered = filtered.Where(x =>
                ContainsIgnoreCase(x.TrackingNumber, term) ||
                ContainsIgnoreCase(x.OrderNumber, term) ||
                ContainsIgnoreCase(x.CustomerName, term) ||
                ContainsIgnoreCase(x.CarrierName, term) ||
                ContainsIgnoreCase(x.PrintedBy, term) ||
                ContainsIgnoreCase(x.ErrorMessage, term));
        }

        return filtered
            .OrderByDescending(x => x.Id)
            .Take(filter.Take)
            .ToList();
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.PrintHistory.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        if (rows == 0)
            throw new InvalidOperationException($"History entry not found: {id}");
    }

    public async Task DeleteManyAsync(IEnumerable<long> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return;

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.PrintHistory.Where(x => idList.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }

    private static bool ContainsIgnoreCase(string? value, string term)
        => !string.IsNullOrEmpty(value)
           && value.Contains(term, StringComparison.OrdinalIgnoreCase);

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
