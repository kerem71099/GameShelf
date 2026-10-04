using GameShelf.Application.Abstractions;

namespace GameShelf.Application.Services;

/// <summary>
/// Kapak görselini uygulamanın yerel metadata klasörüne kopyalar.
/// İnternetten görsel indirmez — kaynak her zaman kullanıcının seçtiği yerel dosyadır.
/// </summary>
public sealed class CoverImageService : ICoverImageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    private readonly ISettingsService _settings;
    private readonly ILoggingService _logger;

    public CoverImageService(ISettingsService settings, ILoggingService logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<string?> ImportAsync(Guid gameId, string sourceFile, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFile))
        {
            return null;
        }

        var extension = Path.GetExtension(sourceFile);
        if (!AllowedExtensions.Contains(extension) || !HasImageSignature(sourceFile))
        {
            _logger.Warning(nameof(CoverImageService), $"Desteklenmeyen kapak dosyası: {sourceFile}");
            return null;
        }

        var targetDirectory = Path.Combine(_settings.Current.MetadataPath, gameId.ToString("N"));
        Directory.CreateDirectory(targetDirectory);

        var destination = Path.Combine(targetDirectory, "cover" + extension.ToLowerInvariant());

        using var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);

        return destination;
    }

    public bool Delete(Guid gameId)
    {
        var directory = Path.Combine(_settings.Current.MetadataPath, gameId.ToString("N"));
        if (!Directory.Exists(directory))
        {
            return false;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(nameof(CoverImageService), $"Kapak silinemedi: {directory} ({ex.Message})");
            return false;
        }
    }

    /// <summary>Gerçekten resim mi? (uzantıya güvenmeyiz; PNG/JPEG imzasına bakarız.)</summary>
    private static bool HasImageSignature(string path)
    {
        try
        {
            var buffer = new byte[12];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length);
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read < 4)
            {
                return false;
            }

            var isPng = buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47;
            var isJpeg = buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF;

            return isPng || isJpeg;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
