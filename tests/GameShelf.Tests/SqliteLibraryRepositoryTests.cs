using GameShelf.Application.Models;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using GameShelf.Infrastructure.Data;
using GameShelf.Infrastructure.Emulators;
using GameShelf.Infrastructure.Repositories;
using Xunit;

namespace GameShelf.Tests;

/// <summary>Gerçek SQLite ile repository testleri (geçici veritabanı dosyası).</summary>
public sealed class SqliteLibraryRepositoryTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteLibraryRepository _repository;

    public SqliteLibraryRepositoryTests()
    {
        var databasePath = Path.Combine(_temp.Path, "test-library.db");
        _factory = new SqliteConnectionFactory(databasePath);

        var backends = new Domain.Interfaces.IEmulatorBackend[]
        {
            new DuckStationBackend(), new Pcsx2Backend(), new Rpcs3Placeholder()
        };

        var factory = new EmulatorBackendFactory(backends, new NullLogger());
        new DbInitializer(_factory, factory, new NullLogger()).InitializeAsync().GetAwaiter().GetResult();

        _repository = new SqliteLibraryRepository(_factory, new NullLogger());
    }

    private sealed class Rpcs3Placeholder : Domain.Interfaces.IEmulatorBackend
    {
        public string Id => "rpcs3";
        public PlatformId PlatformId => PlatformId.Ps3;
        public string DisplayName => "RPCS3";
        public IReadOnlyList<string> SupportedExtensions => [".iso"];
        public bool SupportsDirectoryTargets => true;
        public string DefaultArgumentTemplate => "--no-gui \"{game}\"";
        public string FullscreenArgument => string.Empty;
        public string NoFullscreenArgument => string.Empty;
        public bool RequiresBios => true;
        public string BiosHint => string.Empty;
        public string BuildArguments(Domain.Models.EmulatorLaunchContext context) => DefaultArgumentTemplate;
        public IEnumerable<Domain.Models.LaunchIssue> Validate(Domain.Models.EmulatorLaunchContext context) => [];
    }

    [Fact]
    public async Task Platforms_AreSeeded()
    {
        var platforms = await _repository.GetPlatformsAsync();

        Assert.Equal(6, platforms.Count);
        Assert.Contains(platforms, p => p.Id == PlatformId.Ps3 && p.SupportsDirectory);
        Assert.Contains(platforms, p => p.Id == PlatformId.Ps4 && !p.IsSupported);
    }

    [Fact]
    public async Task UpsertGame_KeepsIdentityOnSecondScan()
    {
        var game = new Game
        {
            Title = "Tekken 3",
            SortTitle = "tekken 3",
            PlatformId = PlatformId.Ps1,
            FilePath = @"D:\games\tekken3.cue",
            AddedAt = DateTimeOffset.Now
        };

        await _repository.UpsertGameAsync(game);

        game.PlayTimeMinutes = 42;
        game.IsFavorite = true;
        await _repository.UpsertGameAsync(game);

        var games = await _repository.GetGamesAsync(new LibraryFilter());

        Assert.Single(games);
        Assert.Equal(game.Id, games[0].Id);
        Assert.Equal(42, games[0].PlayTimeMinutes);
        Assert.True(games[0].IsFavorite);
    }

    [Fact]
    public async Task Filters_SearchFavoritesAndSorting()
    {
        await _repository.UpsertGameAsync(new Game
        {
            Title = "Metal Gear Solid 3", SortTitle = "metal gear solid 3",
            PlatformId = PlatformId.Ps2, FilePath = @"D:\a.cue", IsFavorite = true
        });

        await _repository.UpsertGameAsync(new Game
        {
            Title = "Wipeout", SortTitle = "wipeout",
            PlatformId = PlatformId.Ps1, FilePath = @"D:\b.cue"
        });

        var favoritesOnly = await _repository.GetGamesAsync(new LibraryFilter { FavoritesOnly = true });
        var search = await _repository.GetGamesAsync(new LibraryFilter { Search = "wipe" });
        var byPlatform = await _repository.GetGamesAsync(new LibraryFilter { Platform = PlatformId.Ps2 });

        Assert.Single(favoritesOnly);
        Assert.Single(search);
        Assert.Single(byPlatform);
        Assert.Equal("Wipeout", search[0].Title);
    }

    [Fact]
    public async Task PlayTime_IsIncremented()
    {
        var game = new Game { Title = "Test", PlatformId = PlatformId.Ps1, FilePath = @"D:\t.cue" };
        await _repository.UpsertGameAsync(game);

        await _repository.AddPlayTimeAsync(game.Id, 30);
        await _repository.AddPlayTimeAsync(game.Id, 15);

        var reloaded = await _repository.GetGameAsync(game.Id);

        Assert.Equal(45, reloaded!.PlayTimeMinutes);
    }

    [Fact]
    public async Task LaunchHistory_IsRecordedAndCleared()
    {
        var game = new Game { Title = "Test", PlatformId = PlatformId.Ps3, FilePath = @"D:\ps3\game" };
        await _repository.UpsertGameAsync(game);

        await _repository.AddLaunchHistoryAsync(new LaunchHistoryEntry
        {
            GameId = game.Id,
            PlatformId = PlatformId.Ps3,
            EmulatorPath = @"C:\rpcs3.exe",
            Arguments = "--no-gui \"D:\\ps3\\game\""
        });

        var history = await _repository.GetLaunchHistoryAsync();

        Assert.Single(history);
        Assert.Equal("--no-gui \"D:\\ps3\\game\"", history[0].Arguments);

        await _repository.ClearLaunchHistoryAsync();
        Assert.Empty(await _repository.GetLaunchHistoryAsync());
    }

    [Fact]
    public async Task LibraryFolders_AreStoredAndRemoved()
    {
        var folder = new LibraryFolder { Path = @"D:\games", Recursive = true };
        await _repository.UpsertLibraryFolderAsync(folder);

        Assert.Single(await _repository.GetLibraryFoldersAsync());

        await _repository.DeleteLibraryFolderAsync(folder.Id);
        Assert.Empty(await _repository.GetLibraryFoldersAsync());
    }

    public void Dispose() => _factory.Dispose();
}
