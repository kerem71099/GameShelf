using GameShelf.Application.Models;

namespace GameShelf.Application.Abstractions;

public interface ISettingsService
{
    /// <summary>Bellekteki güncel ayarlar (uygulama boyunca tek örnek).</summary>
    AppSettings Current { get; }

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>Ayarları değiştirir ve hemen diske yazar.</summary>
    Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default);
}
