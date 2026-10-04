using GameShelf.Domain.Enums;
using GameShelf.Domain.Interfaces;
using GameShelf.Domain.Models;
using GameShelf.Infrastructure.Emulators;

namespace GameShelf.Infrastructure.Plugins;

/// <summary>
/// Kod yüklemeden (codeFree) çalışan plugin: tüm davranış manifest'ten gelir.
/// DLL yüklenmediği için ek güvenlik riski yoktur.
/// </summary>
public sealed class ManifestOnlyBackend : IEmulatorBackend
{
    private readonly PluginManifest _manifest;

    public ManifestOnlyBackend(PluginManifest manifest)
    {
        _manifest = manifest;
    }

    public string Id => _manifest.Id;

    public PlatformId PlatformId => (PlatformId)_manifest.PlatformId;

    public string DisplayName => string.IsNullOrWhiteSpace(_manifest.DisplayName)
        ? _manifest.Name
        : _manifest.DisplayName;

    public IReadOnlyList<string> SupportedExtensions => _manifest.SupportedExtensions;

    public bool SupportsDirectoryTargets => _manifest.SupportsDirectoryTargets;

    public string DefaultArgumentTemplate => _manifest.DefaultArgumentTemplate;

    public string FullscreenArgument => _manifest.FullscreenArgument;

    public string NoFullscreenArgument => _manifest.NoFullscreenArgument;

    public bool RequiresBios => _manifest.RequiresBios;

    public string BiosHint => _manifest.BiosHint;

    public string BuildArguments(EmulatorLaunchContext context) =>
        EmulatorArgumentRenderer.Render(
            string.IsNullOrWhiteSpace(context.Emulator.ArgumentTemplate)
                ? DefaultArgumentTemplate
                : context.Emulator.ArgumentTemplate,
            context,
            FullscreenArgument,
            NoFullscreenArgument);

    public IEnumerable<LaunchIssue> Validate(EmulatorLaunchContext context)
    {
        if (RequiresBios && string.IsNullOrWhiteSpace(context.BiosPath))
        {
            yield return LaunchIssue.Warning("bios_not_configured",
                $"{DisplayName} için BIOS/firmware yolu ayarlanmamış.");
        }

        if (File.Exists(context.Game.FilePath) && SupportedExtensions.Count > 0
            && !SupportedExtensions.Contains(Path.GetExtension(context.Game.FilePath), StringComparer.OrdinalIgnoreCase))
        {
            yield return LaunchIssue.Warning("extension_unexpected",
                $"{Path.GetExtension(context.Game.FilePath)} bu plugin için beklenen bir uzantı değil.");
        }
    }
}
