using System.Globalization;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;

namespace GameShelf.Infrastructure.Repositories;

/// <summary>
/// SQLite satırları (yalnızca string/long) ↔ domain entity dönüşümü.
/// Dapper'ın tip dönüşümlerine güvenmek yerine elle eşleme yapıyoruz:
/// böylece tarih/Guid/enum formatları her sürümde deterministik olur.
/// </summary>
public static class RowMappers
{
    // ------------------------------------------------------------------- satırlar

    public sealed class GameRow
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SortTitle { get; set; } = string.Empty;
        public long PlatformId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string? ContentHash { get; set; }
        public string? Region { get; set; }
        public string? CoverPath { get; set; }
        public string? Notes { get; set; }
        public long IsFavorite { get; set; }
        public long IsMissing { get; set; }
        public string AddedAt { get; set; } = string.Empty;
        public string? LastPlayedAt { get; set; }
        public long PlayTimeMinutes { get; set; }
        public string? LibraryFolderId { get; set; }
    }

    public sealed class LibraryFolderRow
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long? PlatformHint { get; set; }
        public long Recursive { get; set; }
        public long IsEnabled { get; set; }
        public string? LastScannedAt { get; set; }
    }

    public sealed class EmulatorConfigRow
    {
        public string Id { get; set; } = string.Empty;
        public long PlatformId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ExecutablePath { get; set; }
        public string? WorkingDirectory { get; set; }
        public string? ArgumentTemplate { get; set; }
        public long DefaultFullscreen { get; set; }
        public string? ExtraArguments { get; set; }
        public string? Notes { get; set; }
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public sealed class GameOverrideRow
    {
        public string GameId { get; set; } = string.Empty;
        public long? Fullscreen { get; set; }
        public string? ExtraArguments { get; set; }
        public string? ArgumentTemplate { get; set; }
        public string? EmulatorConfigId { get; set; }
        public string? Notes { get; set; }
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public sealed class PlatformRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public long IsSupported { get; set; }
        public long RequiresBios { get; set; }
        public string DefaultExtensions { get; set; } = string.Empty;
        public long SupportsDirectory { get; set; }
        public string AccentColorHex { get; set; } = "#6A6F76";
        public long SortOrder { get; set; } = 100;
    }

    public sealed class LaunchHistoryRow
    {
        public string Id { get; set; } = string.Empty;
        public string? GameId { get; set; }
        public long PlatformId { get; set; }
        public string? EmulatorPath { get; set; }
        public string? Arguments { get; set; }
        public string StartedAt { get; set; } = string.Empty;
        public string? EndedAt { get; set; }
        public long? DurationSeconds { get; set; }
        public long? ExitCode { get; set; }
        public long Success { get; set; }
        public string? Message { get; set; }
    }

    // ------------------------------------------------------------------- eşleme

    public static Game ToGame(this GameRow row) => new()
    {
        Id = GuidOrEmpty(row.Id),
        Title = row.Title,
        SortTitle = row.SortTitle,
        PlatformId = (PlatformId)row.PlatformId,
        FilePath = row.FilePath,
        FileSizeBytes = row.FileSizeBytes,
        ContentHash = row.ContentHash,
        Region = row.Region,
        CoverPath = row.CoverPath,
        Notes = row.Notes,
        IsFavorite = row.IsFavorite != 0,
        IsMissing = row.IsMissing != 0,
        AddedAt = ParseDate(row.AddedAt) ?? DateTimeOffset.Now,
        LastPlayedAt = ParseDate(row.LastPlayedAt),
        PlayTimeMinutes = (int)row.PlayTimeMinutes,
        LibraryFolderId = ParseGuid(row.LibraryFolderId)
    };

    public static LibraryFolder ToLibraryFolder(this LibraryFolderRow row) => new()
    {
        Id = GuidOrEmpty(row.Id),
        Path = row.Path,
        PlatformHint = row.PlatformHint is null ? null : (PlatformId)row.PlatformHint.Value,
        Recursive = row.Recursive != 0,
        IsEnabled = row.IsEnabled != 0,
        LastScannedAt = ParseDate(row.LastScannedAt)
    };

    public static EmulatorConfig ToEmulatorConfig(this EmulatorConfigRow row) => new()
    {
        Id = GuidOrEmpty(row.Id),
        PlatformId = (PlatformId)row.PlatformId,
        Name = row.Name,
        ExecutablePath = row.ExecutablePath,
        WorkingDirectory = row.WorkingDirectory,
        ArgumentTemplate = row.ArgumentTemplate,
        DefaultFullscreen = row.DefaultFullscreen != 0,
        ExtraArguments = row.ExtraArguments,
        Notes = row.Notes,
        UpdatedAt = ParseDate(row.UpdatedAt) ?? DateTimeOffset.Now
    };

    public static GameOverride ToGameOverride(this GameOverrideRow row) => new()
    {
        GameId = GuidOrEmpty(row.GameId),
        Fullscreen = row.Fullscreen is null ? null : row.Fullscreen.Value != 0,
        ExtraArguments = row.ExtraArguments,
        ArgumentTemplate = row.ArgumentTemplate,
        EmulatorConfigId = ParseGuid(row.EmulatorConfigId),
        Notes = row.Notes,
        UpdatedAt = ParseDate(row.UpdatedAt) ?? DateTimeOffset.Now
    };

    public static Domain.Entities.Platform ToPlatform(this PlatformRow row) => new()
    {
        Id = (PlatformId)row.Id,
        Name = row.Name,
        ShortName = row.ShortName,
        IsSupported = row.IsSupported != 0,
        RequiresBios = row.RequiresBios != 0,
        DefaultExtensions = row.DefaultExtensions,
        SupportsDirectory = row.SupportsDirectory != 0,
        AccentColorHex = row.AccentColorHex,
        SortOrder = (int)row.SortOrder
    };

    public static LaunchHistoryEntry ToLaunchHistoryEntry(this LaunchHistoryRow row) => new()
    {
        Id = GuidOrEmpty(row.Id),
        GameId = ParseGuid(row.GameId),
        PlatformId = (PlatformId)row.PlatformId,
        EmulatorPath = row.EmulatorPath,
        Arguments = row.Arguments,
        StartedAt = ParseDate(row.StartedAt) ?? DateTimeOffset.Now,
        EndedAt = ParseDate(row.EndedAt),
        DurationSeconds = row.DurationSeconds is null ? null : (int)row.DurationSeconds.Value,
        ExitCode = row.ExitCode is null ? null : (int)row.ExitCode.Value,
        Success = row.Success != 0,
        Message = row.Message
    };

    // ---------------------------------------------------------------- yardımcılar

    public static string? Iso(DateTimeOffset? value) =>
        value?.ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
            ? result
            : null;

    public static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id : null;

    private static Guid GuidOrEmpty(string? value) =>
        Guid.TryParse(value, out var id) ? id : Guid.Empty;
}
