using GameShelf.Domain.Enums;

namespace GameShelf.Domain.Entities;

/// <summary>Kullanıcının taranmasını istediği yerel klasör.</summary>
public sealed class LibraryFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Path { get; set; } = string.Empty;

    /// <summary>Klasör içeriği için platform ipucu (tahmin zorlaştığında kullanılır).</summary>
    public PlatformId? PlatformHint { get; set; }

    public bool Recursive { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? LastScannedAt { get; set; }
}
