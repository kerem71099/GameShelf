using System.ComponentModel;
using System.Diagnostics;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using GameShelf.Infrastructure.Platform;

namespace GameShelf.Tests;

/// <summary>Testler için senkron çalışan dispatcher (UI thread'i yok).</summary>
public sealed class ImmediateDispatcher : IDispatcher
{
    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public void Invoke(Action action) => action();
}

public sealed class NullLogger : ILoggingService
{
    public string LogDirectory => Path.GetTempPath();

    public void Log(LogLevel level, string category, string message, Exception? exception = null)
    {
    }

    public void Debug(string category, string message)
    {
    }

    public void Info(string category, string message)
    {
    }

    public void Warning(string category, string message)
    {
    }

    public void Error(string category, string message, Exception? exception = null)
    {
    }
}

public sealed class FakeSettingsService : ISettingsService
{
    public FakeSettingsService()
    {
        Current = new AppSettings
        {
            DatabasePath = Path.Combine(Path.GetTempPath(), "gameshelf-tests", "library.db"),
            MetadataPath = Path.Combine(Path.GetTempPath(), "gameshelf-tests", "Metadata")
        };
    }

    public AppSettings Current { get; }

    public int SaveCount { get; private set; }

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default)
    {
        mutate(Current);
        SaveCount++;
        return Task.CompletedTask;
    }
}

public sealed class FakeDialogService : IDialogService
{
    public string? NextFile { get; set; }

    public string? NextFolder { get; set; }

    public bool ConfirmResult { get; set; } = true;

    public List<string> Messages { get; } = new();

    public string? OpenFile(string title, string filter, string? initialDirectory = null) => NextFile;

    public string? OpenFolder(string description, string? initialDirectory = null) => NextFolder;

    public bool Confirm(string title, string message) => ConfirmResult;

    public void ShowMessage(string title, string message) => Messages.Add($"{title}: {message}");
}

public sealed class FakeShellService : IShellService
{
    public List<string> Revealed { get; } = new();

    public List<string> Opened { get; } = new();

    public void RevealInExplorer(string path) => Revealed.Add(path);

    public void OpenFolder(string path) => Opened.Add(path);
}

/// <summary>Emülatörü GERÇEKTEN başlatmayan process launcher (launch smoke testi).</summary>
public sealed class FakeLaunchedProcess : ILaunchedProcess
{
    public int Id => 4242;

    public string StartupOutput { get; set; } = string.Empty;

    public int ExitCode { get; init; }

    public bool WasAwaited { get; private set; }

    public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        WasAwaited = true;
        return Task.FromResult(ExitCode);
    }
}

public sealed class FakeProcessLauncher : IProcessLauncher
{
    public ProcessStartInfo? LastStartInfo { get; private set; }

    public int StartCount { get; private set; }

    public bool ShouldThrow { get; set; }

    public FakeLaunchedProcess LastProcess { get; } = new();

    public ILaunchedProcess Start(ProcessStartInfo startInfo)
    {
        if (ShouldThrow)
        {
            throw new Win32Exception(2, "Emülatör başlatılamadı (test).");
        }

        LastStartInfo = startInfo;
        StartCount++;
        return LastProcess;
    }
}

/// <summary>Bellek içi repository: gerçek SQLite'e gerek duymayan testler için.</summary>
public sealed class InMemoryLibraryRepository : ILibraryRepository
{
    private readonly Dictionary<Guid, Game> _games = new();
    private readonly Dictionary<Guid, LibraryFolder> _folders = new();
    private readonly Dictionary<PlatformId, EmulatorConfig> _configs = new();
    private readonly Dictionary<Guid, GameOverride> _overrides = new();
    private readonly List<LaunchHistoryEntry> _history = new();

    public IReadOnlyList<Domain.Entities.Platform> Platforms { get; init; } = PlatformCatalog.Defaults;

    public Task<IReadOnlyList<Game>> GetGamesAsync(LibraryFilter filter, CancellationToken cancellationToken = default)
    {
        IEnumerable<Game> query = _games.Values;

        if (filter.Platform is { } platform)
        {
            query = query.Where(g => g.PlatformId == platform);
        }

        if (filter.FavoritesOnly)
        {
            query = query.Where(g => g.IsFavorite);
        }

        if (!filter.IncludeMissing)
        {
            query = query.Where(g => !g.IsMissing);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(g =>
                g.Title.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) ||
                g.FilePath.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));
        }

        query = filter.SortBy switch
        {
            GameSort.RecentlyPlayed => query.OrderByDescending(g => g.LastPlayedAt),
            GameSort.RecentlyAdded => query.OrderByDescending(g => g.AddedAt),
            GameSort.MostPlayed => query.OrderByDescending(g => g.PlayTimeMinutes),
            _ => query.OrderBy(g => g.SortTitle)
        };

        return Task.FromResult<IReadOnlyList<Game>>(query.Skip(filter.Skip).Take(filter.Take).ToList());
    }

    public Task<Game?> GetGameAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_games.TryGetValue(id, out var game) ? game : null);

    public Task<Game?> GetGameByPathAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(_games.Values.FirstOrDefault(g =>
            string.Equals(g.FilePath, path, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Game>> GetGamesByHashAsync(string contentHash, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Game>>(_games.Values
            .Where(g => string.Equals(g.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
            .ToList());

    public Task UpsertGameAsync(Game game, CancellationToken cancellationToken = default)
    {
        _games[game.Id] = game;
        return Task.CompletedTask;
    }

    public Task DeleteGameAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _games.Remove(id);
        _overrides.Remove(id);
        return Task.CompletedTask;
    }

    public Task SetMissingAsync(IEnumerable<Guid> gameIds, bool missing, CancellationToken cancellationToken = default)
    {
        foreach (var id in gameIds)
        {
            if (_games.TryGetValue(id, out var game))
            {
                game.IsMissing = missing;
            }
        }

        return Task.CompletedTask;
    }

    public Task AddPlayTimeAsync(Guid gameId, int minutes, CancellationToken cancellationToken = default)
    {
        if (_games.TryGetValue(gameId, out var game))
        {
            game.PlayTimeMinutes += minutes;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAllContentHashesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_games.Values
            .Select(g => g.ContentHash!)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct()
            .ToList());

    public Task<IReadOnlyList<LibraryFolder>> GetLibraryFoldersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LibraryFolder>>(_folders.Values.ToList());

    public Task UpsertLibraryFolderAsync(LibraryFolder folder, CancellationToken cancellationToken = default)
    {
        _folders[folder.Id] = folder;
        return Task.CompletedTask;
    }

    public Task DeleteLibraryFolderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _folders.Remove(id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EmulatorConfig>> GetEmulatorConfigsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmulatorConfig>>(_configs.Values.ToList());

    public Task<EmulatorConfig?> GetEmulatorConfigAsync(PlatformId platformId, CancellationToken cancellationToken = default)
        => Task.FromResult(_configs.TryGetValue(platformId, out var config) ? config : null);

    public Task UpsertEmulatorConfigAsync(EmulatorConfig config, CancellationToken cancellationToken = default)
    {
        _configs[config.PlatformId] = config;
        return Task.CompletedTask;
    }

    public Task<GameOverride?> GetGameOverrideAsync(Guid gameId, CancellationToken cancellationToken = default)
        => Task.FromResult(_overrides.TryGetValue(gameId, out var gameOverride) ? gameOverride : null);

    public Task UpsertGameOverrideAsync(GameOverride gameOverride, CancellationToken cancellationToken = default)
    {
        _overrides[gameOverride.GameId] = gameOverride;
        return Task.CompletedTask;
    }

    public Task DeleteGameOverrideAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        _overrides.Remove(gameId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Domain.Entities.Platform>> GetPlatformsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Platforms);

    public Task AddLaunchHistoryAsync(LaunchHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        _history.Add(entry);
        return Task.CompletedTask;
    }

    public Task UpdateLaunchHistoryAsync(LaunchHistoryEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<LaunchHistoryEntry>> GetLaunchHistoryAsync(int take = 200, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LaunchHistoryEntry>>(
            _history.OrderByDescending(h => h.StartedAt).Take(take).ToList());

    public Task ClearLaunchHistoryAsync(CancellationToken cancellationToken = default)
    {
        _history.Clear();
        return Task.CompletedTask;
    }
}
