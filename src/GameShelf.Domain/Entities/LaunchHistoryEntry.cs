using GameShelf.Domain.Enums;

namespace GameShelf.Domain.Entities;

/// <summary>Yerel başlatma kaydı (telemetri değil; internete çıkmaz).</summary>
public sealed class LaunchHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? GameId { get; set; }

    public PlatformId PlatformId { get; set; }

    public string? EmulatorPath { get; set; }

    /// <summary>Üretilen tam komut satırı — sorun giderme için saklanır.</summary>
    public string? Arguments { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? EndedAt { get; set; }

    public int? DurationSeconds { get; set; }

    public int? ExitCode { get; set; }

    /// <summary>false ise süreç başlatılamadı (doğrulama hatası vs.).</summary>
    public bool Success { get; set; } = true;

    public string? Message { get; set; }
}
