using System.Text.Json.Serialization;

namespace GameShelf.Application.Models;

public enum LibraryViewMode
{
    Grid = 0,
    List = 1
}

public enum GameSort
{
    Title = 0,
    RecentlyPlayed = 1,
    RecentlyAdded = 2,
    MostPlayed = 3
}

public sealed class WindowSettings
{
    public double Width { get; set; } = 1280;
    public double Height { get; set; } = 820;
    public bool IsMaximized { get; set; }
}

/// <summary>
/// <c>%LOCALAPPDATA%\GameShelf\settings.json</c> içeriği.
/// Hiçbir ayar internete çıkmaz; tüm değerler yereldir.
/// </summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";          // "Dark" | "Light" | "System"
    public string Accent { get; set; } = "Ember";
    public LibraryViewMode LibraryViewMode { get; set; } = LibraryViewMode.Grid;
    public GameSort SortBy { get; set; } = GameSort.Title;
    public bool ShowUnsupportedPlatforms { get; set; } = true;
    public bool ConfirmBeforeLaunch { get; set; } = true;
    public bool MinimizeOnLaunch { get; set; } = true;

    public string DatabasePath { get; set; } = string.Empty;
    public string MetadataPath { get; set; } = string.Empty;
    public string PluginPath { get; set; } = string.Empty;

    /// <summary>Platform adı → BIOS/firmware yolu (yalnızca kullanıcının verdiği yol; dosya sağlanmaz).</summary>
    public Dictionary<string, string> BiosPaths { get; set; } = new();

    /// <summary>Kullanıcının taramaya eklemek istediği ek uzantılar: ".pbp", ".cso".</summary>
    public List<string> ExtraScanExtensions { get; set; } = new();

    public DateTimeOffset? LastScanAt { get; set; }

    public WindowSettings Window { get; set; } = new();

    [JsonIgnore]
    public string LogDirectory => System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(DatabasePath) ?? ".", "logs");
}
