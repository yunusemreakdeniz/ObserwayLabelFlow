namespace ObserwayLabelFlow.Core.Security;

/// <summary>
/// Kayıtlı oturumu başlangıçta ve çalışma sırasında geçerli tutar; refresh başarısızsa oturumu temizler.
/// </summary>
public interface ISessionService
{
    /// <summary>Uygulama açılışında: oturum yoksa false; varsa süre dolmadan önce/sonra yenilemeyi dener.</summary>
    Task<bool> RestoreOrRefreshAsync(CancellationToken ct = default);

    /// <summary>Ana pencerede periyodik kontrol: gerekirse yenile; başarısızsa false (login’e dön).</summary>
    Task<bool> TryMaintainSessionAsync(CancellationToken ct = default);
}
