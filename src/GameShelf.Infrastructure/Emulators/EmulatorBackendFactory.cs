using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Extensions;
using GameShelf.Domain.Interfaces;

namespace GameShelf.Infrastructure.Emulators;

/// <summary>Platform → backend eşlemesi: built-in adapter'lar + (opsiyonel) manifest plugin'leri.</summary>
public sealed class EmulatorBackendFactory : IEmulatorBackendFactory
{
    private readonly Dictionary<PlatformId, IEmulatorBackend> _backends = new();
    private readonly ILoggingService _logger;
    private readonly IPluginLoader? _pluginLoader;
    private readonly ISettingsService? _settings;

    public EmulatorBackendFactory(
        IEnumerable<IEmulatorBackend> builtIns,
        ILoggingService logger,
        IPluginLoader? pluginLoader = null,
        ISettingsService? settings = null)
    {
        _logger = logger;
        _pluginLoader = pluginLoader;
        _settings = settings;

        foreach (var backend in builtIns)
        {
            Register(backend);
        }
    }

    public IReadOnlyList<IEmulatorBackend> All => _backends.Values.ToList();

    public IEmulatorBackend? Get(PlatformId platformId) =>
        _backends.TryGetValue(platformId, out var backend) ? backend : null;

    public void Register(IEmulatorBackend backend)
    {
        _backends[backend.PlatformId] = backend;
        _logger.Info(nameof(EmulatorBackendFactory), $"Backend kaydedildi: {backend.Id} ({backend.PlatformId})");
    }

    /// <summary>
    /// Manifest plugin'lerini yükler (ps4/ps5 gibi placeholder platformlar için).
    /// Bir plugin hata verirse diğerleri yüklenmeye devam eder.
    /// </summary>
    public void LoadPlugins()
    {
        if (_pluginLoader is null || _settings is null)
        {
            return;
        }

        var root = _settings.Current.PluginPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return;
        }

        IReadOnlyList<IEmulatorBackend> plugins;
        try
        {
            plugins = _pluginLoader.LoadAll(root);
        }
        catch (Exception ex)
        {
            _logger.Error(nameof(EmulatorBackendFactory), "Plugin'ler yüklenemedi.", ex);
            return;
        }

        foreach (var plugin in plugins)
        {
            // Built-in adapter'ları ezme: yalnızca desteklenmeyen platformlara izin ver.
            if (Get(plugin.PlatformId) is { } existing && existing.PlatformId.IsLaunchable())
            {
                _logger.Warning(nameof(EmulatorBackendFactory),
                    $"{plugin.PlatformId} için built-in backend varken plugin yoksayıldı: {plugin.Id}");
                continue;
            }

            Register(plugin);
        }
    }
}
