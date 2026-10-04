using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using System.Diagnostics;

namespace GameShelf.Application.Services;

/// <summary>
/// Kullanıcının eklediği klasörleri recursive tarar, platform tahmin eder,
/// parmak izi çıkarır (duplicate tespiti) ve kütüphaneye yazar.
/// Tamamen yereldir: internete çıkmaz, dosya indirmez, dosya kopyalamaz.
/// </summary>
public sealed class GameScannerService : IGameScannerService
{
    private readonly ILibraryRepository _repository;
    private readonly IPlatformDetector _detector;
    private readonly IFileHasher _hasher;
    private readonly ISettingsService _settings;
    private readonly ILoggingService _logger;

    public GameScannerService(
        ILibraryRepository repository,
        IPlatformDetector detector,
        IFileHasher hasher,
        ISettingsService settings,
        ILoggingService logger)
    {
        _repository = repository;
        _detector = detector;
        _hasher = hasher;
        _settings = settings;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(
        IReadOnlyList<LibraryFolder> folders,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ScanResult();
        var extensions = BuildExtensionSet();
        var knownHashes = new HashSet<string>(
            await _repository.GetAllContentHashesAsync(cancellationToken).ConfigureAwait(false),
            StringComparer.OrdinalIgnoreCase);

        var candidates = new List<string>();
        var ownerByCandidate = new Dictionary<string, LibraryFolder>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders.Where(f => f.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(folder.Path))
            {
                result.Errors.Add($"Klasör bulunamadı: {folder.Path}");
                continue;
            }

            CollectCandidates(folder, extensions, candidates, ownerByCandidate, result, cancellationToken);
        }

        var processed = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            progress?.Report(new ScanProgress(processed, candidates.Count, candidate));

            var folder = ownerByCandidate.TryGetValue(candidate, out var owner) ? owner : null;

            try
            {
                await ProcessCandidateAsync(candidate, folder, knownHashes, result, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                result.Errors.Add($"{candidate}: {ex.Message}");
                _logger.Warning(nameof(GameScannerService), $"{candidate}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                result.Errors.Add($"{candidate}: erişim yok ({ex.Message})");
                _logger.Warning(nameof(GameScannerService), $"{candidate}: erişim yok");
            }
        }

        result.Missing = await MarkMissingGamesAsync(cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        _logger.Info(nameof(GameScannerService), $"Tarama tamamlandı: {result}");
        return result;
    }

    public Task<PlatformDetection> DetectPlatformAsync(
        string path,
        PlatformId? hint = null,
        CancellationToken cancellationToken = default)
        => _detector.DetectAsync(path, hint, cancellationToken);

    // ------------------------------------------------------------------ tarama

    private void CollectCandidates(
        LibraryFolder folder,
        HashSet<string> extensions,
        List<string> candidates,
        Dictionary<string, LibraryFolder> ownerByCandidate,
        ScanResult result,
        CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = folder.Recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
            ReturnSpecialDirectories = false
        };

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder.Path, "*.*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!extensions.Contains(Path.GetExtension(file)))
                {
                    continue;
                }

                if (ownerByCandidate.TryAdd(file, folder))
                {
                    candidates.Add(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            result.Errors.Add($"{folder.Path}: {ex.Message}");
        }

        // PS3 gibi klasör tabanlı oyunlar (PS3_GAME / PS3_DISC.SFB / PARAM.SFO + USRDIR)
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(folder.Path, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isGameDirectory = await _detector
                    .IsGameDirectoryAsync(directory, cancellationToken)
                    .ConfigureAwait(false);

                if (isGameDirectory && ownerByCandidate.TryAdd(directory, folder))
                {
                    candidates.Add(directory);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            result.Errors.Add($"{folder.Path}: {ex.Message}");
        }
    }

    private async Task ProcessCandidateAsync(
        string path,
        LibraryFolder? folder,
        HashSet<string> knownHashes,
        ScanResult result,
        CancellationToken cancellationToken)
    {
        var detection = await _detector
            .DetectAsync(path, folder?.PlatformHint, cancellationToken)
            .ConfigureAwait(false);

        if (detection.PlatformId == PlatformId.Unknown)
        {
            result.NeedsReview++;
        }

        var hash = await _hasher.ComputeAsync(path, cancellationToken).ConfigureAwait(false);
        var size = GetSize(path);

        var existing = await _repository.GetGameByPathAsync(path, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            // Duplicate: aynı parmak izine sahip farklı bir dosya zaten kayıtlı mı?
            if (!string.IsNullOrEmpty(hash) && knownHashes.Contains(hash))
            {
                result.Duplicates++;
                return;
            }

            var title = TitleCleaner.Clean(path);
            var game = new Game
            {
                Title = title,
                SortTitle = TitleCleaner.ToSortTitle(title),
                PlatformId = detection.PlatformId,
                FilePath = path,
                FileSizeBytes = size,
                ContentHash = hash,
                AddedAt = DateTimeOffset.Now,
                LibraryFolderId = folder?.Id
            };

            await _repository.UpsertGameAsync(game, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(hash))
            {
                knownHashes.Add(hash);
            }

            result.Added++;
            return;
        }

        var changed = existing.FileSizeBytes != size
                      || !string.Equals(existing.ContentHash, hash, StringComparison.OrdinalIgnoreCase)
                      || existing.IsMissing
                      || (existing.PlatformId == PlatformId.Unknown && detection.PlatformId != PlatformId.Unknown);

        if (!changed)
        {
            result.Unchanged++;
            return;
        }

        existing.FileSizeBytes = size;
        existing.ContentHash = hash;
        existing.IsMissing = false;

        if (existing.PlatformId == PlatformId.Unknown && detection.PlatformId != PlatformId.Unknown)
        {
            existing.PlatformId = detection.PlatformId;
        }

        if (folder is not null)
        {
            existing.LibraryFolderId = folder.Id;
        }

        await _repository.UpsertGameAsync(existing, cancellationToken).ConfigureAwait(false);
        result.Updated++;
    }

    private async Task<int> MarkMissingGamesAsync(CancellationToken cancellationToken)
    {
        var all = await _repository.GetGamesAsync(new LibraryFilter(), cancellationToken).ConfigureAwait(false);
        var missingIds = all.Where(g => !Exists(g.FilePath)).Select(g => g.Id).ToList();

        if (missingIds.Count > 0)
        {
            await _repository.SetMissingAsync(missingIds, true, cancellationToken).ConfigureAwait(false);
        }

        return missingIds.Count;
    }

    private static bool Exists(string path)
        => !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path));

    private static long GetSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private HashSet<string> BuildExtensionSet()
    {
        var set = new HashSet<string>(_detector.KnownExtensions, StringComparer.OrdinalIgnoreCase);

        foreach (var extra in _settings.Current.ExtraScanExtensions)
        {
            if (extra.StartsWith('.'))
            {
                set.Add(extra.ToLowerInvariant());
            }
        }

        return set;
    }
}
