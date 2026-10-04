namespace GameShelf.Application.Models;

/// <summary>
/// Bir emülatörün RESMÎ indirme kaynağı.
/// GameShelf hiçbir emülatör ikili dosyasını dağıtmaz; yalnızca resmî adresi gösterir.
/// </summary>
public sealed record EmulatorDownloadInfo(
    string PlatformKey,
    string PlatformName,
    string EmulatorName,
    string DownloadUrl,
    string Hint);
