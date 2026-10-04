using System.Text.Json.Serialization;

namespace GameShelf.Infrastructure.Plugins;

/// <summary>Plugin yetkileri. Hepsi varsayılan olarak kapalıdır.</summary>
public sealed class PluginCapabilities
{
    /// <summary>Offline garantisi: ağ erişimi her koşulda reddedilir.</summary>
    public bool Network { get; set; }

    public bool FileSystemOutsideLibrary { get; set; }

    /// <summary>Backend zaten tek bir emülatör süreci başlatır.</summary>
    public bool ExecuteArbitraryProcess { get; set; } = true;
}

public sealed class PluginAssemblyInfo
{
    public string File { get; set; } = string.Empty;

    public string TypeName { get; set; } = string.Empty;

    /// <summary>DLL'in SHA-256 değeri (küçük harf hex). Uyuşmazsa plugin YÜKLENMEZ.</summary>
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>
/// <c>plugins/&lt;id&gt;/plugin.json</c> şeması.
/// Yasal beyanlar (declaresNoDrmBypass / declaresNoRomDistribution / declaresOfflineOnly)
/// zorunludur; bunlardan biri false ise plugin reddedilir (bkz. LEGAL.md).
/// </summary>
public sealed class PluginManifest
{
    public int SchemaVersion { get; set; } = 1;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public int PlatformId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    // ---- zorunlu yasal beyanlar
    public bool DeclaresNoDrmBypass { get; set; }

    public bool DeclaresNoRomDistribution { get; set; }

    public bool DeclaresOfflineOnly { get; set; }

    public PluginCapabilities Capabilities { get; set; } = new();

    /// <summary>true ise hiçbir DLL yüklenmez (önerilen mod).</summary>
    public bool CodeFree { get; set; } = true;

    public List<string> SupportedExtensions { get; set; } = new();

    public bool SupportsDirectoryTargets { get; set; }

    public string DefaultArgumentTemplate { get; set; } = string.Empty;

    public string FullscreenArgument { get; set; } = string.Empty;

    public string NoFullscreenArgument { get; set; } = string.Empty;

    public bool RequiresBios { get; set; }

    public string BiosHint { get; set; } = string.Empty;

    [JsonPropertyName("assembly")]
    public PluginAssemblyInfo? Assembly { get; set; }
}
