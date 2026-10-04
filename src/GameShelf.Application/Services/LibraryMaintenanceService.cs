using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Domain.Entities;

namespace GameShelf.Application.Services;

/// <summary>Tools ekranının ihtiyaç duyduğu bakım işlemleri (kayıp dosya, içe aktarılmamış dosya, yeniden tarama).</summary>
public sealed class LibraryMaintenanceService
{
    private readonly ILibraryRepository _repository;
    private readonly IGameScannerService _scanner;
    private readonly ISettingsService _settings;
    private readonly ILoggingService _logger;

    public LibraryMaintenanceService(
        ILibraryRepository repository,
        IGameScannerService scanner,
        ISettingsService settings,
        ILoggingService logger)
    {
        _repository = repository;
        _scanner = scanner;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Veritabanında olup diskte bulunamayan oyunlar.</summary>
    public async Task<IReadOnlyList<Game>> FindMissingGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = await _repository.GetGamesAsync(new LibraryFilter(), cancellationToken).ConfigureAwait(false);
        var missing = games
            .Where(g => !File.Exists(g.FilePath) && !Directory.Exists(g.FilePath))
            .ToList();

        if (missing.Count > 0)
        {
            await _repository
                .SetMissingAsync(missing.Select(g => g.Id), true, cancellationToken)
                .ConfigureAwait(false);
        }

        return missing;
    }

    /// <summary>Kütüphane klasörlerinde olup veritabanına eklenmemiş dosyalar (duplicate'ler hariç).</summary>
    public async Task<IReadOnlyList<string>> FindUnimportedFilesAsync(CancellationToken cancellationToken = default)
    {
        var folders = await _repository.GetLibraryFoldersAsync(cancellationToken).ConfigureAwait(false);
        var known = new HashSet<string>(
            (await _repository.GetGamesAsync(new LibraryFilter(), cancellationToken).ConfigureAwait(false))
                .Select(g => g.FilePath),
            StringComparer.OrdinalIgnoreCase);

        var extensions = BuildExtensionSet();
        var found = new List<string>();

        foreach (var folder in folders.Where(f => f.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(folder.Path))
            {
                continue;
            }

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = folder.Recursive,
                IgnoreInaccessible = true,
                AttributesToSkip = 0
            };

            try
            {
                foreach (var file in Directory.EnumerateFiles(folder.Path, "*.*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (extensions.Contains(Path.GetExtension(file)) && !known.Contains(file))
                    {
                        found.Add(file);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.Warning(nameof(LibraryMaintenanceService), $"{folder.Path}: {ex.Message}");
            }
        }

        return found;
    }

    public async Task<ScanResult> RescanAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var folders = await _repository.GetLibraryFoldersAsync(cancellationToken).ConfigureAwait(false);
        var result = await _scanner.ScanAsync(folders, progress, cancellationToken).ConfigureAwait(false);

        await _settings.UpdateAsync(s => s.LastScanAt = DateTimeOffset.Now, cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task RemoveGamesAsync(IEnumerable<Guid> gameIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in gameIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _repository.DeleteGameAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    private HashSet<string> BuildExtensionSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cue", ".bin", ".iso", ".chd", ".pbp", ".cso", ".m3u" };

        foreach (var extra in _settings.Current.ExtraScanExtensions)
        {
            if (extra.StartsWith('.'))
            {
                set.Add(extra);
            }
        }

        return set;
    }
}
