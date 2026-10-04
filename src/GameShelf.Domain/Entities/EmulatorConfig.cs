using GameShelf.Domain.Enums;

namespace GameShelf.Domain.Entities;

/// <summary>
/// Bir platform için emülatör yapılandırması.
/// Emülatörün kendisini GameShelf DAĞITMAZ; kullanıcı kendi kurulumunun exe yolunu verir.
/// </summary>
public sealed class EmulatorConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public PlatformId PlatformId { get; set; }

    /// <summary>Örn. "DuckStation".</summary>
    public string Name { get; set; } = string.Empty;

    public string? ExecutablePath { get; set; }

    /// <summary>Boşsa emülatör exe'sinin bulunduğu dizin kullanılır.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Argüman şablonu. Token'lar: {game} {fullscreen} {extra} {bios}.
    /// Boş/null ise backend'in <c>DefaultArgumentTemplate</c> değeri kullanılır.
    /// </summary>
    public string? ArgumentTemplate { get; set; }

    public bool DefaultFullscreen { get; set; } = true;

    /// <summary>Şablonda {extra} yerine geçer (örn. "-fastboot").</summary>
    public string? ExtraArguments { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
