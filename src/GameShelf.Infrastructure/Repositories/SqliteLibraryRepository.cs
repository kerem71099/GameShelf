using Dapper;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using GameShelf.Infrastructure.Data;
using System.Data;

namespace GameShelf.Infrastructure.Repositories;

/// <summary>
/// SQLite + Dapper erişim katmanı.
/// Tüm tarihler ISO-8601 metin, tüm Id'ler Guid metin olarak saklanır (bkz. RowMappers).
/// </summary>
public sealed class SqliteLibraryRepository : ILibraryRepository
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly ILoggingService _logger;

    public SqliteLibraryRepository(ISqliteConnectionFactory factory, ILoggingService logger)
    {
        _factory = factory;
        _logger = logger;
    }

    // ================================================================ Games

    public async Task<IReadOnlyList<Game>> GetGamesAsync(LibraryFilter filter, CancellationToken cancellationToken = default)
    {
        var order = OrderClause(filter.SortBy);

        var sql = $"""
            SELECT * FROM Games
            WHERE (@Platform IS NULL OR PlatformId = @Platform)
              AND (@FavoritesOnly = 0 OR IsFavorite = 1)
              AND (@IncludeMissing = 1 OR IsMissing = 0)
              AND (@Search IS NULL OR Title LIKE @Search ESCAPE '\' OR FilePath LIKE @Search ESCAPE '\')
            ORDER BY {order}
            LIMIT @Take OFFSET @Skip;
            """;

        using var connection = _factory.Create();

        var rows = await connection.QueryAsync<RowMappers.GameRow>(new CommandDefinition(sql, new
        {
            Platform = filter.Platform is null ? null : (int?)filter.Platform.Value,
            FavoritesOnly = filter.FavoritesOnly ? 1 : 0,
            IncludeMissing = filter.IncludeMissing ? 1 : 0,
            Search = LikeTerm(filter.Search),
            filter.Take,
            filter.Skip
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => r.ToGame()).ToList();
    }

    public async Task<Game?> GetGameAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        var row = await connection.QueryFirstOrDefaultAsync<RowMappers.GameRow>(new CommandDefinition(
            "SELECT * FROM Games WHERE Id = @Id;",
            new { Id = id.ToString() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row?.ToGame();
    }

    public async Task<Game?> GetGameByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        var row = await connection.QueryFirstOrDefaultAsync<RowMappers.GameRow>(new CommandDefinition(
            "SELECT * FROM Games WHERE FilePath = @FilePath COLLATE NOCASE LIMIT 1;",
            new { FilePath = path },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row?.ToGame();
    }

    public async Task<IReadOnlyList<Game>> GetGamesByHashAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        var rows = await connection.QueryAsync<RowMappers.GameRow>(new CommandDefinition(
            "SELECT * FROM Games WHERE ContentHash = @Hash;",
            new { Hash = contentHash },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => r.ToGame()).ToList();
    }

    public async Task UpsertGameAsync(Game game, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Games (Id, Title, SortTitle, PlatformId, FilePath, FileSizeBytes, ContentHash,
                               Region, CoverPath, Notes, IsFavorite, IsMissing, AddedAt, LastPlayedAt,
                               PlayTimeMinutes, LibraryFolderId)
            VALUES (@Id, @Title, @SortTitle, @PlatformId, @FilePath, @FileSizeBytes, @ContentHash,
                    @Region, @CoverPath, @Notes, @IsFavorite, @IsMissing, @AddedAt, @LastPlayedAt,
                    @PlayTimeMinutes, @LibraryFolderId)
            ON CONFLICT(FilePath) DO UPDATE SET
                Title           = excluded.Title,
                SortTitle       = excluded.SortTitle,
                PlatformId      = excluded.PlatformId,
                FileSizeBytes   = excluded.FileSizeBytes,
                ContentHash     = excluded.ContentHash,
                Region          = excluded.Region,
                CoverPath       = excluded.CoverPath,
                Notes           = excluded.Notes,
                IsFavorite      = excluded.IsFavorite,
                IsMissing       = excluded.IsMissing,
                LastPlayedAt    = excluded.LastPlayedAt,
                PlayTimeMinutes = excluded.PlayTimeMinutes,
                LibraryFolderId = excluded.LibraryFolderId;
            """;

        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = game.Id.ToString(),
            game.Title,
            game.SortTitle,
            PlatformId = (int)game.PlatformId,
            game.FilePath,
            game.FileSizeBytes,
            game.ContentHash,
            game.Region,
            game.CoverPath,
            game.Notes,
            IsFavorite = game.IsFavorite ? 1 : 0,
            IsMissing = game.IsMissing ? 1 : 0,
            AddedAt = RowMappers.Iso(game.AddedAt),
            LastPlayedAt = RowMappers.Iso(game.LastPlayedAt),
            game.PlayTimeMinutes,
            LibraryFolderId = game.LibraryFolderId?.ToString()
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task DeleteGameAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Games WHERE Id = @Id;",
            new { Id = id.ToString() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task SetMissingAsync(IEnumerable<Guid> gameIds, bool missing, CancellationToken cancellationToken = default)
    {
        var ids = gameIds.Select(id => id.ToString()).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Games SET IsMissing = @Missing WHERE Id IN @Ids;",
            new { Missing = missing ? 1 : 0, Ids = ids },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task AddPlayTimeAsync(Guid gameId, int minutes, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Games SET PlayTimeMinutes = PlayTimeMinutes + @Minutes WHERE Id = @Id;",
            new { Minutes = minutes, Id = gameId.ToString() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetAllContentHashesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        var hashes = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT DISTINCT ContentHash FROM Games WHERE ContentHash IS NOT NULL AND ContentHash <> '';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return hashes.ToList();
    }

    // ======================================================= LibraryFolders

    public async Task<IReadOnlyList<LibraryFolder>> GetLibraryFoldersAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        var rows = await connection.QueryAsync<RowMappers.LibraryFolderRow>(new CommandDefinition(
            "SELECT * FROM LibraryFolders ORDER BY Path;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => r.ToLibraryFolder()).ToList();
    }

    public async Task UpsertLibraryFolderAsync(LibraryFolder folder, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO LibraryFolders (Id, Path, PlatformHint, Recursive, IsEnabled, LastScannedAt)
            VALUES (@Id, @Path, @PlatformHint, @Recursive, @IsEnabled, @LastScannedAt)
            ON CONFLICT(Path) DO UPDATE SET
                PlatformHint  = excluded.PlatformHint,
                Recursive     = excluded.Recursive,
                IsEnabled     = excluded.IsEnabled,
                LastScannedAt = excluded.LastScannedAt;
            """;

        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = folder.Id.ToString(),
            folder.Path,
            PlatformHint = folder.PlatformHint is null ? null : (int?)folder.PlatformHint.Value,
            Recursive = folder.Recursive ? 1 : 0,
            IsEnabled = folder.IsEnabled ? 1 : 0,
            LastScannedAt = RowMappers.Iso(folder.LastScannedAt)
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task DeleteLibraryFolderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM LibraryFolders WHERE Id = @Id;",
            new { Id = id.ToString() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    // ====================================================== EmulatorConfigs

    public async Task<IReadOnlyList<EmulatorConfig>> GetEmulatorConfigsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        var rows = await connection.QueryAsync<RowMappers.EmulatorConfigRow>(new CommandDefinition(
            "SELECT * FROM EmulatorConfigs ORDER BY PlatformId;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => r.ToEmulatorConfig()).ToList();
    }

    public async Task<EmulatorConfig?> GetEmulatorConfigAsync(PlatformId platformId, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        var row = await connection.QueryFirstOrDefaultAsync<RowMappers.EmulatorConfigRow>(new CommandDefinition(
            "SELECT * FROM EmulatorConfigs WHERE PlatformId = @PlatformId;",
            new { PlatformId = (int)platformId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row?.ToEmulatorConfig();
    }

    public async Task UpsertEmulatorConfigAsync(EmulatorConfig config, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO EmulatorConfigs (Id, PlatformId, Name, ExecutablePath, WorkingDirectory,
                                         ArgumentTemplate, DefaultFullscreen, ExtraArguments, Notes, UpdatedAt)
            VALUES (@Id, @PlatformId, @Name, @ExecutablePath, @WorkingDirectory,
                    @ArgumentTemplate, @DefaultFullscreen, @ExtraArguments, @Notes, @UpdatedAt)
            ON CONFLICT(PlatformId) DO UPDATE SET
                Name              = excluded.Name,
                ExecutablePath    = excluded.ExecutablePath,
                WorkingDirectory  = excluded.WorkingDirectory,
                ArgumentTemplate  = excluded.ArgumentTemplate,
                DefaultFullscreen = excluded.DefaultFullscreen,
                ExtraArguments    = excluded.ExtraArguments,
                Notes             = excluded.Notes,
                UpdatedAt         = excluded.UpdatedAt;
            """;

        config.UpdatedAt = DateTimeOffset.Now;

        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = config.Id.ToString(),
            PlatformId = (int)config.PlatformId,
            config.Name,
            config.ExecutablePath,
            config.WorkingDirectory,
            config.ArgumentTemplate,
            DefaultFullscreen = config.DefaultFullscreen ? 1 : 0,
            config.ExtraArguments,
            config.Notes,
            UpdatedAt = RowMappers.Iso(config.UpdatedAt)
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    // ========================================================= GameOverrides

    public async Task<GameOverride?> GetGameOverrideAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        var row = await connection.QueryFirstOrDefaultAsync<RowMappers.GameOverrideRow>(new CommandDefinition(
            "SELECT * FROM GameOverrides WHERE GameId = @GameId;",
            new { GameId = gameId.ToString() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row?.ToGameOverride();
    }

    public async Task UpsertGameOverrideAsync(GameOverride gameOverride, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO GameOverrides (GameId, Fullscreen, ExtraArguments, ArgumentTemplate,
                                       EmulatorConfigId, Notes, UpdatedAt)
            VALUES (@GameId, @Fullscreen, @ExtraArguments, @ArgumentTemplate,
                    @EmulatorConfigId, @Notes, @UpdatedAt)
            ON CONFLICT(GameId) DO UPDATE SET
                Fullscreen       = excluded.Fullscreen,
                ExtraArguments   = excluded.ExtraArguments,
                ArgumentTemplate = excluded.ArgumentTemplate,
                EmulatorConfigId = excluded.EmulatorConfigId,
                Notes            = excluded.Notes,
                UpdatedAt        = excluded.UpdatedAt;
            """;

        gameOverride.UpdatedAt = DateTimeOffset.Now;

        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            GameId = gameOverride.GameId.ToString(),
            Fullscreen = gameOverride.Fullscreen is null ? null : (gameOverride.Fullscreen.Value ? 1 : 0),
            gameOverride.ExtraArguments,
            gameOverride.ArgumentTemplate,
            EmulatorConfigId = gameOverride.EmulatorConfigId?.ToString(),
            gameOverride.Notes,
            UpdatedAt = RowMappers.Iso(gameOverride.UpdatedAt)
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task DeleteGameOverrideAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM GameOverrides WHERE GameId = @GameId;",
            new { GameId = gameId.ToString() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    // ============================================================ Platforms

    public async Task<IReadOnlyList<Domain.Entities.Platform>> GetPlatformsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        var rows = await connection.QueryAsync<RowMappers.PlatformRow>(new CommandDefinition(
            "SELECT * FROM Platforms ORDER BY SortOrder, Id;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => r.ToPlatform()).ToList();
    }

    // ======================================================== LaunchHistory

    public async Task AddLaunchHistoryAsync(LaunchHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO LaunchHistory (Id, GameId, PlatformId, EmulatorPath, Arguments, StartedAt,
                                       EndedAt, DurationSeconds, ExitCode, Success, Message)
            VALUES (@Id, @GameId, @PlatformId, @EmulatorPath, @Arguments, @StartedAt,
                    @EndedAt, @DurationSeconds, @ExitCode, @Success, @Message);
            """;

        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(sql, Parameters(entry), cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task UpdateLaunchHistoryAsync(LaunchHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE LaunchHistory SET
                EndedAt         = @EndedAt,
                DurationSeconds = @DurationSeconds,
                ExitCode        = @ExitCode,
                Success         = @Success,
                Message         = @Message
            WHERE Id = @Id;
            """;

        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(sql, Parameters(entry), cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LaunchHistoryEntry>> GetLaunchHistoryAsync(int take = 200, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        var rows = await connection.QueryAsync<RowMappers.LaunchHistoryRow>(new CommandDefinition(
            "SELECT * FROM LaunchHistory ORDER BY StartedAt DESC LIMIT @Take;",
            new { Take = take },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => r.ToLaunchHistoryEntry()).ToList();
    }

    public async Task ClearLaunchHistoryAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM LaunchHistory;", cancellationToken: cancellationToken)).ConfigureAwait(false);

        _logger.Info(nameof(SqliteLibraryRepository), "Launch log temizlendi.");
    }

    // ========================================================= yardımcılar

    private static object Parameters(LaunchHistoryEntry entry) => new
    {
        Id = entry.Id.ToString(),
        GameId = entry.GameId?.ToString(),
        PlatformId = (int)entry.PlatformId,
        entry.EmulatorPath,
        entry.Arguments,
        StartedAt = RowMappers.Iso(entry.StartedAt),
        EndedAt = RowMappers.Iso(entry.EndedAt),
        DurationSeconds = entry.DurationSeconds is null ? null : (int?)entry.DurationSeconds.Value,
        ExitCode = entry.ExitCode is null ? null : (int?)entry.ExitCode.Value,
        Success = entry.Success ? 1 : 0,
        entry.Message
    };

    private static string OrderClause(GameSort sortBy) => sortBy switch
    {
        GameSort.RecentlyPlayed => "LastPlayedAt DESC, SortTitle ASC",
        GameSort.RecentlyAdded => "AddedAt DESC, SortTitle ASC",
        GameSort.MostPlayed => "PlayTimeMinutes DESC, SortTitle ASC",
        _ => "SortTitle ASC"
    };

    private static string? LikeTerm(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var escaped = term.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
