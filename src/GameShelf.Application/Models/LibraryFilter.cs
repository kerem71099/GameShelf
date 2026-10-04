using GameShelf.Domain.Enums;

namespace GameShelf.Application.Models;

/// <summary>Kütüphane listesi sorgu filtresi.</summary>
public sealed class LibraryFilter
{
    public PlatformId? Platform { get; set; }

    /// <summary>Başlık veya dosya yolunda arama (LIKE %..%).</summary>
    public string? Search { get; set; }

    public bool FavoritesOnly { get; set; }

    public bool IncludeMissing { get; set; } = true;

    public GameSort SortBy { get; set; } = GameSort.Title;

    public int Skip { get; set; }

    public int Take { get; set; } = 10_000;
}
