using Dapper;
using GameShelf.Application.Abstractions;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Extensions;
using GameShelf.Infrastructure.Platform;
using System.Data;

namespace GameShelf.Infrastructure.Data;

/// <summary>Şemayı oluşturur ve Platforms/EmulatorConfigs tohum verisini yazar.</summary>
public sealed class DbInitializer
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IEmulatorBackendFactory _backends;
    private readonly ILoggingService _logger;

    public DbInitializer(
        ISqliteConnectionFactory factory,
        IEmulatorBackendFactory backends,
        ILoggingService logger)
    {
        _factory = factory;
        _backends = backends;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Create();

        foreach (var statement in Schema.Statements)
        {
            await connection.ExecuteAsync(new CommandDefinition(statement, cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        await SeedPlatformsAsync(connection, cancellationToken).ConfigureAwait(false);
        await SeedEmulatorConfigsAsync(connection, cancellationToken).ConfigureAwait(false);
        await SetMetaAsync(connection, "SchemaVersion", Schema.CurrentVersion.ToString(), cancellationToken)
            .ConfigureAwait(false);

        _logger.Info(nameof(DbInitializer), $"Veritabanı hazır (şema v{Schema.CurrentVersion}).");
    }

    private static async Task SeedPlatformsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Platforms (Id, Name, ShortName, IsSupported, RequiresBios,
                                   DefaultExtensions, SupportsDirectory, AccentColorHex, SortOrder)
            VALUES (@Id, @Name, @ShortName, @IsSupported, @RequiresBios,
                    @DefaultExtensions, @SupportsDirectory, @AccentColorHex, @SortOrder)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                ShortName = excluded.ShortName,
                IsSupported = excluded.IsSupported,
                RequiresBios = excluded.RequiresBios,
                DefaultExtensions = excluded.DefaultExtensions,
                SupportsDirectory = excluded.SupportsDirectory,
                AccentColorHex = excluded.AccentColorHex,
                SortOrder = excluded.SortOrder;
            """;

        foreach (var platform in PlatformCatalog.Defaults)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = (int)platform.Id,
                platform.Name,
                platform.ShortName,
                IsSupported = platform.IsSupported ? 1 : 0,
                RequiresBios = platform.RequiresBios ? 1 : 0,
                platform.DefaultExtensions,
                SupportsDirectory = platform.SupportsDirectory ? 1 : 0,
                platform.AccentColorHex,
                platform.SortOrder
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    private async Task SeedEmulatorConfigsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO EmulatorConfigs (Id, PlatformId, Name, ExecutablePath, WorkingDirectory,
                                         ArgumentTemplate, DefaultFullscreen, ExtraArguments, Notes, UpdatedAt)
            VALUES (@Id, @PlatformId, @Name, NULL, NULL, NULL, 1, NULL, NULL, @UpdatedAt)
            ON CONFLICT(PlatformId) DO NOTHING;
            """;

        foreach (var platformId in new[] { PlatformId.Ps1, PlatformId.Ps2, PlatformId.Ps3 })
        {
            var backend = _backends.Get(platformId);

            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = Guid.NewGuid().ToString(),
                PlatformId = (int)platformId,
                Name = backend?.DisplayName ?? platformId.ToShortName(),
                UpdatedAt = DateTimeOffset.Now.ToString("O")
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    private static Task SetMetaAsync(IDbConnection connection, string key, string value, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO SchemaMeta (Key, Value) VALUES (@Key, @Value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """,
            new { Key = key, Value = value },
            cancellationToken: cancellationToken));
}
