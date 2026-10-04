using GameShelf.Domain.Enums;

namespace GameShelf.Domain.Entities;

/// <summary>
/// Platform tanımı (veritabanındaki <c>Platforms</c> tablosunun karşılığı).
/// PS4/PS5 kayıtları <see cref="IsSupported"/> = false ile placeholder olarak durur.
/// </summary>
public sealed class Platform
{
    public PlatformId Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    /// <summary>Launcher bu platformu başlatabilir mi? PS4/PS5 için false.</summary>
    public bool IsSupported { get; set; }

    /// <summary>Bu platform için BIOS/firmware yolu gerekli mi? (Sadece kontrol edilir.)</summary>
    public bool RequiresBios { get; set; }

    /// <summary>Noktalı, küçük harf: ".iso,.chd".</summary>
    public string DefaultExtensions { get; set; } = string.Empty;

    /// <summary>PS3 gibi klasör hedefi kabul eden platformlar için true.</summary>
    public bool SupportsDirectory { get; set; }

    public string AccentColorHex { get; set; } = "#6A6F76";

    public int SortOrder { get; set; } = 100;

    public IReadOnlyList<string> Extensions => DefaultExtensions
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
