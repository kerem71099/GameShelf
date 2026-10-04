using GameShelf.Application.Models;

namespace GameShelf.Infrastructure.OS;

/// <summary>
/// Uygulamanın tüm dosya yolları tek yerde.
/// Varsayılan: %LOCALAPPDATA%\GameShelf\  (Portable mod v1: exe'nin yanında portable.txt)
/// </summary>
public static class AppPaths
{
    public static string AppDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameShelf");

    public static string DefaultDatabasePath => Path.Combine(AppDataDirectory, "library.db");

    public static string DefaultMetadataPath => Path.Combine(AppDataDirectory, "Metadata");

    public static string DefaultPluginPath => Path.Combine(AppDataDirectory, "plugins");

    public static string LogDirectory => Path.Combine(AppDataDirectory, "logs");

    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");

    /// <summary>Ayarlardaki boş yolları varsayılanlarla doldurur ve klasörleri oluşturur.</summary>
    public static AppSettings EnsureDefaults(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DatabasePath))
        {
            settings.DatabasePath = DefaultDatabasePath;
        }

        if (string.IsNullOrWhiteSpace(settings.MetadataPath))
        {
            settings.MetadataPath = DefaultMetadataPath;
        }

        if (string.IsNullOrWhiteSpace(settings.PluginPath))
        {
            settings.PluginPath = DefaultPluginPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(settings.DatabasePath)!);
        Directory.CreateDirectory(settings.MetadataPath);
        Directory.CreateDirectory(settings.PluginPath);
        Directory.CreateDirectory(LogDirectory);

        return settings;
    }
}
