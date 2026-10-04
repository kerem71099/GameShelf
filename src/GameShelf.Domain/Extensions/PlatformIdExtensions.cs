using GameShelf.Domain.Enums;

namespace GameShelf.Domain.Extensions;

/// <summary>PlatformId için görüntüleme yardımcıları (UI ve log metinleri).</summary>
public static class PlatformIdExtensions
{
    public static string ToShortName(this PlatformId id) => id switch
    {
        PlatformId.Ps1 => "PS1",
        PlatformId.Ps2 => "PS2",
        PlatformId.Ps3 => "PS3",
        PlatformId.Ps4 => "PS4",
        PlatformId.Ps5 => "PS5",
        _ => "?"
    };

    public static string ToDisplayName(this PlatformId id) => id switch
    {
        PlatformId.Ps1 => "PlayStation",
        PlatformId.Ps2 => "PlayStation 2",
        PlatformId.Ps3 => "PlayStation 3",
        PlatformId.Ps4 => "PlayStation 4",
        PlatformId.Ps5 => "PlayStation 5",
        _ => "Bilinmeyen"
    };

    /// <summary>Launcher tarafından başlatılabilir mi? (PS4/PS5 desteklenmez.)</summary>
    public static bool IsLaunchable(this PlatformId id) => id is PlatformId.Ps1 or PlatformId.Ps2 or PlatformId.Ps3;
}
