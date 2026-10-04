namespace GameShelf.Domain.Entities;

/// <summary>Tek bir oyun için emülatör ayarlarını ezme (override).</summary>
public sealed class GameOverride
{
    public Guid GameId { get; set; }

    /// <summary>null = emülatörün/platformun varsayılanını kullan.</summary>
    public bool? Fullscreen { get; set; }

    /// <summary>Oyuna özel ek argümanlar; {extra} token'ına yazılır.</summary>
    public string? ExtraArguments { get; set; }

    /// <summary>Oyuna özel tam argüman şablonu (opsiyonel).</summary>
    public string? ArgumentTemplate { get; set; }

    /// <summary>Farklı bir emülatör yapılandırması kullanılacaksa (ileri seviye).</summary>
    public Guid? EmulatorConfigId { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
