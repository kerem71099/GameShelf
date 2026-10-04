using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;

namespace GameShelf.Infrastructure.Platform;

/// <summary>
/// Platform tohum verisi. PS4/PS5 yalnızca UI placeholder'ıdır: emülasyon sağlanmaz.
/// </summary>
public static class PlatformCatalog
{
    public static IReadOnlyList<Domain.Entities.Platform> Defaults { get; } =
    [
        new()
        {
            Id = PlatformId.Unknown,
            Name = "Bilinmeyen",
            ShortName = "?",
            IsSupported = false,
            RequiresBios = false,
            DefaultExtensions = string.Empty,
            SupportsDirectory = false,
            AccentColorHex = "#6A6F76",
            SortOrder = 999
        },
        new()
        {
            Id = PlatformId.Ps1,
            Name = "PlayStation",
            ShortName = "PS1",
            IsSupported = true,
            RequiresBios = true,
            DefaultExtensions = ".cue,.bin,.iso,.chd,.pbp,.m3u,.ecm,.mds,.ccd",
            SupportsDirectory = false,
            AccentColorHex = "#6E8B9E",
            SortOrder = 10
        },
        new()
        {
            Id = PlatformId.Ps2,
            Name = "PlayStation 2",
            ShortName = "PS2",
            IsSupported = true,
            RequiresBios = true,
            DefaultExtensions = ".iso,.chd,.cso,.bin,.cue",
            SupportsDirectory = false,
            AccentColorHex = "#5F6BB0",
            SortOrder = 20
        },
        new()
        {
            Id = PlatformId.Ps3,
            Name = "PlayStation 3",
            ShortName = "PS3",
            IsSupported = true,
            RequiresBios = true,
            DefaultExtensions = string.Empty, // klasör tabanlı: PS3_GAME / PS3_DISC.SFB
            SupportsDirectory = true,
            AccentColorHex = "#8D6A9F",
            SortOrder = 30
        },
        new()
        {
            Id = PlatformId.Ps4,
            Name = "PlayStation 4",
            ShortName = "PS4",
            IsSupported = false, // emülasyon sağlanmaz
            RequiresBios = false,
            DefaultExtensions = string.Empty,
            SupportsDirectory = false,
            AccentColorHex = "#6A6F76",
            SortOrder = 40
        },
        new()
        {
            Id = PlatformId.Ps5,
            Name = "PlayStation 5",
            ShortName = "PS5",
            IsSupported = false, // emülasyon sağlanmaz
            RequiresBios = false,
            DefaultExtensions = string.Empty,
            SupportsDirectory = false,
            AccentColorHex = "#6A6F76",
            SortOrder = 50
        }
    ];

    /// <summary>Tarama sırasında dikkate alınacak tüm uzantılar.</summary>
    public static IReadOnlyList<string> AllExtensions { get; } = Defaults
        .SelectMany(p => p.Extensions)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
