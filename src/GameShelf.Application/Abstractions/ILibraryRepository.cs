using GameShelf.Domain.Entities;
using GameShelf.Application.Models;
using GameShelf.Domain.Enums;

namespace GameShelf.Application.Abstractions;

public interface ILibraryRepository
{
    // ---------------------------------------------------------------- Games
    Task<IReadOnlyList<Game>> GetGamesAsync(LibraryFilter filter, CancellationToken cancellationToken = default);

    Task<Game?> GetGameAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Game?> GetGameByPathAsync(string path, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Game>> GetGamesByHashAsync(string contentHash, CancellationToken cancellationToken = default);

    /// <summary>INSERT ... ON CONFLICT(FilePath) DO UPDATE — aynı dosya tekrar taranınca Id korunur.</summary>
    Task UpsertGameAsync(Game game, CancellationToken cancellationToken = default);

    Task DeleteGameAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetMissingAsync(IEnumerable<Guid> gameIds, bool missing, CancellationToken cancellationToken = default);

    Task AddPlayTimeAsync(Guid gameId, int minutes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAllContentHashesAsync(CancellationToken cancellationToken = default);

    // ------------------------------------------------------- LibraryFolders
    Task<IReadOnlyList<LibraryFolder>> GetLibraryFoldersAsync(CancellationToken cancellationToken = default);

    Task UpsertLibraryFolderAsync(LibraryFolder folder, CancellationToken cancellationToken = default);

    Task DeleteLibraryFolderAsync(Guid id, CancellationToken cancellationToken = default);

    // ------------------------------------------------------ EmulatorConfigs
    Task<IReadOnlyList<EmulatorConfig>> GetEmulatorConfigsAsync(CancellationToken cancellationToken = default);

    Task<EmulatorConfig?> GetEmulatorConfigAsync(PlatformId platformId, CancellationToken cancellationToken = default);

    Task UpsertEmulatorConfigAsync(EmulatorConfig config, CancellationToken cancellationToken = default);

    // -------------------------------------------------------- GameOverrides
    Task<GameOverride?> GetGameOverrideAsync(Guid gameId, CancellationToken cancellationToken = default);

    Task UpsertGameOverrideAsync(GameOverride gameOverride, CancellationToken cancellationToken = default);

    Task DeleteGameOverrideAsync(Guid gameId, CancellationToken cancellationToken = default);

    // ------------------------------------------------------------ Platforms
    Task<IReadOnlyList<Domain.Entities.Platform>> GetPlatformsAsync(CancellationToken cancellationToken = default);

    // -------------------------------------------------------- LaunchHistory
    Task AddLaunchHistoryAsync(LaunchHistoryEntry entry, CancellationToken cancellationToken = default);

    Task UpdateLaunchHistoryAsync(LaunchHistoryEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LaunchHistoryEntry>> GetLaunchHistoryAsync(int take = 200, CancellationToken cancellationToken = default);

    Task ClearLaunchHistoryAsync(CancellationToken cancellationToken = default);
}
