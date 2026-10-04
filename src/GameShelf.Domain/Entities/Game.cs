namespace GameShelf.Domain.Entities;

/// <summary>
/// Kullanıcının kütüphanesindeki tek bir oyun kaydı.
/// <para>
/// ÖNEMLİ: Bu sınıf yalnızca <c>FilePath</c> saklar. GameShelf oyun dosyasını kopyalamaz,
/// indirmez, paylaşmaz veya doğrulamaz; kullanıcının kendi yasal dump'ına işaret eder.
/// </para>
/// </summary>
public sealed class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    /// <summary>Sıralama için normalize edilmiş başlık (küçük harf, noktalama temizlenmiş).</summary>
    public string SortTitle { get; set; } = string.Empty;

    public Enums.PlatformId PlatformId { get; set; } = Enums.PlatformId.Unknown;

    /// <summary>Oyun dosyasının tam yolu (PS3 için klasör yolu da olabilir).</summary>
    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Hızlı parmak izi: <c>boyut:ilkParçaHash:sonParçaHash</c>.
    /// Duplicate tespiti için kullanılır (tam dosya hash değil; ISO'lar GB boyutunda).
    /// </summary>
    public string? ContentHash { get; set; }

    /// <summary>"EU" / "US" / "JP" — kullanıcı elle girer (offline metadata).</summary>
    public string? Region { get; set; }

    /// <summary><c>Metadata/&lt;id&gt;/cover.png</c> — kullanıcı seçtiğinde doldurulur.</summary>
    public string? CoverPath { get; set; }

    public string? Notes { get; set; }

    public bool IsFavorite { get; set; }

    /// <summary>Dosya diskte bulunamadı (taşınmış/silinmiş).</summary>
    public bool IsMissing { get; set; }

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? LastPlayedAt { get; set; }

    public int PlayTimeMinutes { get; set; }

    public Guid? LibraryFolderId { get; set; }
}
