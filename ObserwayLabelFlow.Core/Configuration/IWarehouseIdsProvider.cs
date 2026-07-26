namespace ObserwayLabelFlow.Core.Configuration;

public interface IWarehouseIdsProvider
{
    long GetTexasWarehouseId();

    long GetMexicoWarehouseId();

    Task ReloadAsync(CancellationToken cancellationToken = default);
}
