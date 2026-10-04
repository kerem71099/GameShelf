using GameShelf.Domain.Entities;

namespace GameShelf.Domain.Models;

/// <summary>
/// Bir oyunun başlatılması için gereken tüm girdiler.
/// Backend'ler bu bağlamdan argüman üretir ve doğrulama yapar.
/// </summary>
public sealed class EmulatorLaunchContext
{
    public required Game Game { get; init; }

    public required EmulatorConfig Emulator { get; init; }

    /// <summary>Oyuna özel override (yoksa null).</summary>
    public GameOverride? GameOverride { get; init; }

    /// <summary>Kullanıcının ayarlarda verdiği BIOS/firmware yolu (yalnızca doğrulama için; dosya sağlanmaz).</summary>
    public string? BiosPath { get; init; }

    /// <summary>Override → emülatör varsayılanı sırasıyla çözülmüş tam ekran durumu.</summary>
    public bool Fullscreen { get; init; }

    /// <summary>Override → emülatör ExtraArguments sırasıyla çözülmüş ek argümanlar.</summary>
    public string? ExtraArguments { get; init; }

    /// <summary>Platform destekli mi (PS4/PS5 için false).</summary>
    public bool PlatformSupported { get; init; } = true;
}
