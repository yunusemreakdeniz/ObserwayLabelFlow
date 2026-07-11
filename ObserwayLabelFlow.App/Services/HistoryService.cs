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

    public async Task<IReadOnlyList<PrintHistoryEntry>> GetRecentAsync(int take = 200, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 2000);
        await using var db = await factory.CreateDbContextAsync(ct);
        // SQLite: DateTimeOffset ORDER BY is not translated; Id is autoincrement so newest rows have largest Id.
        return await db.PrintHistory
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PrintHistoryEntry>> GetAsync(HistoryFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Take = Math.Clamp(filter.Take, 1, 2000);

        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.PrintHistory.AsNoTracking();

        if (filter.OnlyErrors)
            query = query.Where(x => !x.Success);

        if (filter.FromDateUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc >= filter.FromDateUtc.Value);

        if (filter.ToDateUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc <= filter.ToDateUtc.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var term = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.TrackingNumber != null && x.TrackingNumber.ToLower().Contains(term)) ||
                (x.OrderNumber != null && x.OrderNumber.ToLower().Contains(term)) ||
                (x.CustomerName != null && x.CustomerName.ToLower().Contains(term)) ||
                (x.CarrierName != null && x.CarrierName.ToLower().Contains(term)));
        }

        return await query
            .OrderByDescending(x => x.Id)
            .Take(filter.Take)
            .ToListAsync(ct);
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
}
