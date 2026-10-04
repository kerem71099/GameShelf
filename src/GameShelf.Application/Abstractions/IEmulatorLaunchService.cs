using GameShelf.Application.Models;
using GameShelf.Domain.Entities;

namespace GameShelf.Application.Abstractions;

public interface IEmulatorLaunchService
{
    /// <summary>Başlatma öncesi kontroller (exe var mı, dosya var mı, BIOS yolu tanımlı mı ...).</summary>
    Task<LaunchValidation> ValidateAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>Oyunu uygun emülatörle başlatır (yalnızca Process.Start).</summary>
    Task<LaunchOutcome> LaunchAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>UI'da göstermek için üretilecek komut satırının önizlemesi.</summary>
    Task<string?> BuildCommandPreviewAsync(Game game, CancellationToken cancellationToken = default);
}
