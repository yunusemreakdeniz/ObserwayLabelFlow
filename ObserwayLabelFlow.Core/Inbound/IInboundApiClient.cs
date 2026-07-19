using ObserwayLabelFlow.Core.Common;

namespace ObserwayLabelFlow.Core.Inbound;

public interface IInboundApiClient
{
    /// <summary>
    /// OBS sipariş numarası veya satın alma takip numarası ile depo girişini işaretler.
    /// </summary>
    Task<Result<InboundMarkResult>> MarkInboundReceivedAsync(string reference, CancellationToken ct = default);
}
