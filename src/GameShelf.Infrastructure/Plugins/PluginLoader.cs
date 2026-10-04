using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameShelf.Application.Abstractions;
using GameShelf.Domain.Interfaces;

namespace GameShelf.Infrastructure.Plugins;

/// <summary>
/// Manifest tabanlı plugin yükleyici.
/// Güvenlik sırası: JSON doğrula → yasal beyanlar → yetkiler → (DLL ise) SHA-256 → tip kontrolü.
/// Bir plugin hata verirse diğerleri yüklenmeye devam eder; uygulama çökmez.
/// </summary>
public sealed partial class PluginLoader : IPluginLoader
{
    private readonly ILoggingService _logger;
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public PluginLoader(ILoggingService logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<IEmulatorBackend> LoadAll(string pluginRoot)
    {
        var loaded = new List<IEmulatorBackend>();

        if (!Directory.Exists(pluginRoot))
        {
            return loaded;
        }

        foreach (var directory in Directory.EnumerateDirectories(pluginRoot))
        {
            var manifestPath = Path.Combine(directory, "plugin.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = JsonSerializer.Deserialize<PluginManifest>(
                    File.ReadAllText(manifestPath), _options);

                if (manifest is null)
                {
                    _logger.Warning(nameof(PluginLoader), $"Manifest okunamadı: {manifestPath}");
                    continue;
                }

                if (!PassesPolicy(manifest, out var reason))
                {
                    _logger.Warning(nameof(PluginLoader), $"Plugin reddedildi ({manifest.Id}): {reason}");
                    continue;
                }

                if (manifest.CodeFree || manifest.Assembly is null)
                {
                    loaded.Add(new ManifestOnlyBackend(manifest));
                    _logger.Info(nameof(PluginLoader), $"Plugin yüklendi (kodsuz): {manifest.Id}");
                    continue;
                }

                var backend = LoadAssemblyBackend(directory, manifest);
                if (backend is not null)
                {
                    loaded.Add(backend);
                    _logger.Info(nameof(PluginLoader), $"Plugin yüklendi (dll): {manifest.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(nameof(PluginLoader), $"Plugin yüklenemedi: {directory}", ex);
            }
        }

        return loaded;
    }

    // ------------------------------------------------------------------ politika

    private static bool PassesPolicy(PluginManifest manifest, out string reason)
    {
        reason = string.Empty;

        if (manifest.SchemaVersion != 1)
        {
            reason = $"desteklenmeyen şema sürümü ({manifest.SchemaVersion})";
            return false;
        }

        if (!IdRegex().IsMatch(manifest.Id))
        {
            reason = "geçersiz plugin id (yalnızca küçük harf, rakam, nokta ve tire)";
            return false;
        }

        if (!Enum.IsDefined(typeof(Domain.Enums.PlatformId), manifest.PlatformId))
        {
            reason = $"geçersiz platformId ({manifest.PlatformId})";
            return false;
        }

        // Zorunlu yasal beyanlar (LEGAL.md)
        if (!manifest.DeclaresNoDrmBypass)
        {
            reason = "declaresNoDrmBypass=true değil (DRM/koruma atlatma yasak)";
            return false;
        }

        if (!manifest.DeclaresNoRomDistribution)
        {
            reason = "declaresNoRomDistribution=true değil (ROM/ISO dağıtımı yasak)";
            return false;
        }

        if (!manifest.DeclaresOfflineOnly)
        {
            reason = "declaresOfflineOnly=true değil (uygulama offline kalmalı)";
            return false;
        }

        if (manifest.Capabilities.Network)
        {
            reason = "ağ erişimi isteyen plugin'lere izin verilmiyor";
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ dll modu

    private IEmulatorBackend? LoadAssemblyBackend(string directory, PluginManifest manifest)
    {
        var root = Path.GetFullPath(directory);
        var assemblyPath = Path.GetFullPath(Path.Combine(root, manifest.Assembly!.File));

        // Path traversal engeli: DLL plugin klasörünün içinde olmalı.
        if (!assemblyPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warning(nameof(PluginLoader), $"{manifest.Id}: DLL plugin klasörünün dışında.");
            return null;
        }

        if (!File.Exists(assemblyPath))
        {
            _logger.Warning(nameof(PluginLoader), $"{manifest.Id}: DLL bulunamadı ({assemblyPath}).");
            return null;
        }

        var actualHash = ComputeSha256(assemblyPath);
        if (!string.Equals(actualHash, manifest.Assembly.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warning(nameof(PluginLoader),
                $"{manifest.Id}: SHA-256 uyuşmuyor (beklenen {manifest.Assembly.Sha256}, bulunan {actualHash}).");
            return null;
        }

        try
        {
            var context = new AssemblyLoadContext($"gameshelf-plugin-{manifest.Id}", isCollectible: true);
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            var type = assembly.GetType(manifest.Assembly.TypeName, throwOnError: false);

            if (type is null || !typeof(IEmulatorBackend).IsAssignableFrom(type))
            {
                _logger.Warning(nameof(PluginLoader),
                    $"{manifest.Id}: {manifest.Assembly.TypeName} IEmulatorBackend uygulamıyor.");
                return null;
            }

            return Activator.CreateInstance(type) as IEmulatorBackend;
        }
        catch (Exception ex)
        {
            _logger.Error(nameof(PluginLoader), $"{manifest.Id}: DLL yüklenemedi.", ex);
            return null;
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    [GeneratedRegex(@"^[a-z0-9.\-]+$")]
    private static partial Regex IdRegex();
}
