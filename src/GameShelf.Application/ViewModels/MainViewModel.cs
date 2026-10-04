using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Services;
using GameShelf.Domain.Entities;

namespace GameShelf.Application.ViewModels;

public sealed record NavItem(string Key, string Label, string Glyph);

/// <summary>Ana pencere: sol gezinme + içerik (Library / GameDetails / Settings / Tools).</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly EmulatorAutoSetupService _autoSetup;

    public MainViewModel(
        LibraryViewModel library,
        GameDetailsViewModel details,
        SettingsViewModel settingsView,
        ToolsViewModel tools,
        DownloadsViewModel downloads,
        ISettingsService settings,
        EmulatorAutoSetupService autoSetup,
        ILoggingService logger,
        IDispatcher dispatcher)
        : base(logger, dispatcher)
    {
        Library = library;
        Details = details;
        SettingsView = settingsView;
        Tools = tools;
        Downloads = downloads;
        _settings = settings;
        _autoSetup = autoSetup;

        NavItems =
        [
            new NavItem("library", "Kütüphane", "▦"),
            new NavItem("settings", "Ayarlar", "⚙"),
            new NavItem("tools", "Araçlar", "🛠"),
            new NavItem("downloads", "İndirmeler", "↓")
        ];

        Library.DetailsRequested += (_, game) => ShowGameDetails(game);
        Library.SettingsRequested += (_, _) => ShowSettings();
        Details.BackRequested += (_, _) => ShowLibrary();
    }

    public LibraryViewModel Library { get; }

    public GameDetailsViewModel Details { get; }

    public SettingsViewModel SettingsView { get; }

    public ToolsViewModel Tools { get; }

    public DownloadsViewModel Downloads { get; }

    public ObservableCollection<NavItem> NavItems { get; }

    /// <summary>Ctrl+F için: MainWindow arama kutusuna odaklanır.</summary>
    public event EventHandler? FocusSearchRequested;

    /// <summary>Tema değişti: MainWindow tema sözlüğünü yeniden yükler.</summary>
    public event EventHandler<string>? ThemeChanged;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private NavItem? _selectedNav;

    // ------------------------------------------------------------------ yaşam döngüsü

    /// <summary>
    /// Açılışta kütüphaneyi gösterir. `SelectedNav` ataması `OnSelectedNavChanged` üzerinden
    /// gezinmeyi tetikler; ekran hâlâ boşsa doğrudan yüklenir.
    /// </summary>
    public async Task InitializeAsync()
    {
        Logger.Info(nameof(MainViewModel), $"GameShelf {BuildStamp.Display} açılıyor.");

        SelectedNav ??= NavItems.FirstOrDefault(n => n.Key == "library");

        if (CurrentView is null)
        {
            await ShowLibraryInternalAsync().ConfigureAwait(false);
        }

        // Kullanıcı uğraşmasın: emülatör ve BIOS yollarını arka planda kendimiz buluruz.
        _ = RunAutoSetupAsync();
    }

    /// <summary>
    /// Açılışta emülatör exe'si ile BIOS/firmware klasörünü kullanıcının yerine arar.
    /// Hiçbir şey indirmez: diskte zaten kurulu olanları bulup yolunu kaydeder.
    /// </summary>
    private async Task RunAutoSetupAsync()
    {
        try
        {
            var report = await _autoSetup.RunAsync().ConfigureAwait(false);

            Logger.Info(nameof(MainViewModel),
                "Otomatik kurulum: " + (report.Changed
                    ? report.Summary.ReplaceLineEndings(" | ")
                    : "yeni bir şey bulunamadı"));

            if (report.Changed)
            {
                var text = report.Summary.ReplaceLineEndings(" · ");
                await Dispatcher.InvokeAsync(() => StatusMessage = text).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(nameof(MainViewModel), "Otomatik kurulum başarısız.", ex);
        }
    }

    // ------------------------------------------------------------------ gezinme

    /// <summary>Ctrl+F: MainWindow arama kutusuna odaklanmak için tetikler.</summary>
    [RelayCommand]
    private void FocusSearch()
        => RequestSearchFocus();

    [RelayCommand]
    private void Navigate(NavItem? item)
    {
        switch (item?.Key)
        {
            case "settings":
                ShowSettings();
                break;
            case "tools":
                ShowTools();
                break;
            case "downloads":
                ShowDownloads();
                break;
            default:
                ShowLibrary();
                break;
        }
    }

    [RelayCommand]
    private void ShowLibrary()
    {
        SelectedNav = NavItems.FirstOrDefault(n => n.Key == "library");
        CurrentView = Library;
        _ = Library.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowSettings()
    {
        SelectedNav = NavItems.FirstOrDefault(n => n.Key == "settings");
        CurrentView = SettingsView;
        _ = SettingsView.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowTools()
    {
        SelectedNav = NavItems.FirstOrDefault(n => n.Key == "tools");
        CurrentView = Tools;
        _ = Tools.LoadHistoryCommand.ExecuteAsync(null);
        _ = Tools.RefreshSetupCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowDownloads()
    {
        SelectedNav = NavItems.FirstOrDefault(n => n.Key == "downloads");
        CurrentView = Downloads;
        _ = Downloads.RefreshCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var next = string.Equals(_settings.Current.Theme, "Dark", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";

        _settings.Current.Theme = next;
        _ = _settings.SaveAsync();
        ThemeChanged?.Invoke(this, next);
        StatusMessage = $"Tema: {(next == "Dark" ? "Koyu" : "Açık")}";
    }

    public void ShowGameDetails(Game game)
    {
        Details.Load(game);
        SelectedNav = null;
        CurrentView = Details;
    }

    public void RequestSearchFocus()
        => FocusSearchRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Sol rayden seçim değişince gezin. Detay ekranında SelectedNav = null olur
    /// (bu yüzden null değer navigasyon tetiklemez).
    /// </summary>
    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value is not null)
        {
            Navigate(value);
        }
    }

    private async Task ShowLibraryInternalAsync()
    {
        SelectedNav = NavItems.FirstOrDefault(n => n.Key == "library");
        CurrentView = Library;
        await Library.LoadCommand.ExecuteAsync(null).ConfigureAwait(false);
    }
}
