using GameShelf.Application.Models;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;

namespace GameShelf.Application.Abstractions;

public interface IGameScannerService
{
    /// <summary>
    /// Verilen kütüphane klasörlerini tarar, yeni oyunları ekler, var olanları günceller,
    /// kayıp dosyaları işaretler. Tamamen yereldir; internete çıkmaz.
    /// </summary>
    Task<ScanResult> ScanAsync(
        IReadOnlyList<LibraryFolder> folders,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Tek bir yol için platform tahmini (kullanıcı düzeltmesi öncesi önizleme).</summary>
    Task<PlatformDetection> DetectPlatformAsync(
        string path,
        PlatformId? hint = null,
        CancellationToken cancellationToken = default);
}
