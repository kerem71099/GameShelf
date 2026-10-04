using System.Text.Json;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;

namespace GameShelf.Infrastructure.Settings;

/// <summary>
/// <c>settings.json</c> yükleyici/kaydedici.
/// Bozuk dosya durumunda varsayılanlara döner (uygulama her zaman açılır).
/// Yazma atomiktir: geçici dosya → File.Move(overwrite).
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private readonly string _path;
    private readonly ILoggingService _logger;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private AppSettings _current = new();

    public JsonSettingsService(string path, ILoggingService logger)
    {
        _path = path;
        _logger = logger;
    }

    public AppSettings Current => _current;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            _current = new AppSettings();
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            return _current;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
            _current = JsonSerializer.Deserialize<AppSettings>(json, _options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.Warning(nameof(JsonSettingsService), $"Ayarlar okunamadı, varsayılanlar kullanılıyor: {ex.Message}");
            _current = new AppSettings();
        }

        if (string.IsNullOrWhiteSpace(_current.Theme))
        {
            _current.Theme = "Dark";
        }

        return _current;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = _path + ".tmp";
        var json = JsonSerializer.Serialize(_current, _options);

        await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);
        File.Move(temp, _path, overwrite: true);
    }

    public async Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        mutate(_current);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}
