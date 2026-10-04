namespace GameShelf.Application.Models;

public enum DownloadKind
{
    /// <summary>İndirilen dosya açılıp bir klasöre çıkarılır (zip/7z).</summary>
    Extract,

    /// <summary>İndirilen dosya bir kurulum programıdır; kullanıcı onayıyla çalıştırılır.</summary>
    Installer
}

/// <summary>
/// İndirme Merkezi'ndeki tek bir kalem. Yalnızca RESMÎ kaynaklar.
/// GameShelf emülatör/BIOS/oyun DAĞITMAZ; bu listedeki her şey kullanıcının
/// kendi tercihiyle, resmî adreslerden indirilir.
/// </summary>
public sealed record DownloadCatalogItem(
    string Id,
    string Category,
    string Title,
    string Description,
    DownloadKind Kind,
    string? PlatformKey,
    string? Owner,
    string? Repo,
    string? AssetPattern,
    string? AssetFallback,
    string? DirectUrl,
    string PageUrl);
