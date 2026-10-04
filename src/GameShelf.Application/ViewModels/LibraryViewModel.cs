using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Application.Services;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;

namespace GameShelf.Application.ViewModels;

public sealed partial class PlatformFilterItem : ObservableObject
{
    public PlatformId? Platform { get; init; }

    public string Label { get; init; } = string.Empty;

    [ObservableProperty]
    private int _count;

    public override string ToString() => Label;
}

public sealed record GameSortItem(GameSort Value, string Label);

/// <summary>Kütüphane ekranı: arama, filtre, sıralama, grid/list, başlatma, tarama.</summary>
public sealed partial class LibraryViewModel : ViewModelBase
{
    private readonly ILibraryRepository _repository;
    private readonly ISettingsService _settings;
    private readonly IEmulatorLaunchService _launchService;
    private readonly LibraryMaintenanceService _maintenance;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly EmulatorAutoSetupService _autoSetup;

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _scanCts;

    public LibraryViewModel(
        ILibraryRepository repository,
        ISettingsService settings,
        IEmulatorLaunchService launchService,
        LibraryMaintenanceService maintenance,
        IDialogService dialogs,
        IShellService shell,
        EmulatorAutoSetupService autoSetup,
        ILoggingService logger,
        IDispatcher dispatcher)
        : base(logger, dispatcher)
    {
        _repository = repository;
        _settings = settings;
        _launchService = launchService;
        _maintenance = maintenance;
        _dialogs = dialogs;
        _shell = shell;
        _autoSetup = autoSetup;

        ViewMode = settings.Current.LibraryViewMode;
        SortBy = settings.Current.SortBy;

        SortOptions =
        [
            new GameSortItem(GameSort.Title, "Başlık (A–Z)"),
            new GameSortItem(GameSort.RecentlyPlayed, "Son oynanan"),
            new GameSortItem(GameSort.RecentlyAdded, "Yeni eklenen"),
            new GameSortItem(GameSort.MostPlayed, "En çok oynanan")
        ];
    }

    public ObservableCollection<GameItemViewModel> Games { get; } = new();

    public ObservableCollection<PlatformFilterItem> PlatformFilters { get; } = new();

    public ObservableCollection<GameSortItem> SortOptions { get; }

    /// <summary>Detay ekranına geçiş isteği (MainViewModel dinler).</summary>
    public event EventHandler<Game>? DetailsRequested;

    [ObservableProperty]
    private GameItemViewModel? _selectedGame;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private LibraryViewMode _viewMode = LibraryViewMode.Grid;

    [ObservableProperty]
    private GameSort _sortBy = GameSort.Title;

    [ObservableProperty]
    private PlatformFilterItem? _selectedPlatform;

    [ObservableProperty]
    private int _visibleCount;

    [ObservableProperty]
    private double _scanProgress;

    /// <summary>Grid görünümü aktif mi? (XAML iki listeyi bununla gösterir/gizler.)</summary>
    public bool IsGridView => ViewMode == LibraryViewMode.Grid;

    public string ViewModeText => ViewMode == LibraryViewMode.Grid ? "▦ Grid" : "☰ Liste";

    /// <summary>Ayarlar ekranına geçiş isteği (boş durum butonu; MainViewModel dinler).</summary>
    public event EventHandler? SettingsRequested;

    // ------------------------------------------------------------------ yükleme

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunBusyAsync(async ct =>
        {
            await LoadPlatformFiltersAsync(ct).ConfigureAwait(false);
            await LoadGamesAsync(ct).ConfigureAwait(false);
        }, "Kütüphane yükleniyor...");
    }

    private async Task LoadPlatformFiltersAsync(CancellationToken ct)
    {
        var platforms = await _repository.GetPlatformsAsync(ct).ConfigureAwait(false);
        var all = await _repository.GetGamesAsync(new LibraryFilter(), ct).ConfigureAwait(false);

        var items = new List<PlatformFilterItem>
        {
            new() { Platform = null, Label = "Tümü", Count = all.Count }
        };

        foreach (var platform in platforms
                     .Where(p => p.Id != PlatformId.Unknown)
                     .OrderBy(p => p.SortOrder))
        {
            if (!_settings.Current.ShowUnsupportedPlatforms && !platform.IsSupported)
            {
                continue;
            }

            items.Add(new PlatformFilterItem
            {
                Platform = platform.Id,
                Label = platform.ShortName,
                Count = all.Count(g => g.PlatformId == platform.Id)
            });
        }

        await Dispatcher.InvokeAsync(() =>
        {
            PlatformFilters.Clear();
            foreach (var item in items)
            {
                PlatformFilters.Add(item);
            }

            SelectedPlatform = PlatformFilters.FirstOrDefault();
        });
    }

    private async Task LoadGamesAsync(CancellationToken ct)
    {
        var filter = new LibraryFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            Platform = SelectedPlatform?.Platform,
            FavoritesOnly = FavoritesOnly,
            SortBy = SortBy
        };

        var games = await _repository.GetGamesAsync(filter, ct).ConfigureAwait(false);

        await Dispatcher.InvokeAsync(() =>
        {
            Games.Clear();

            foreach (var game in games)
            {
                Games.Add(new GameItemViewModel(game, Logger, Dispatcher));
            }

            VisibleCount = Games.Count;
        });
    }

    // ------------------------------------------------------------------ komutlar

    [RelayCommand]
    private async Task LaunchAsync(GameItemViewModel? item)
    {
        var target = item ?? SelectedGame;
        if (target is null)
        {
            return;
        }

        if (!target.IsLaunchable)
        {
            ErrorMessage = $"{target.PlatformShort} bu sürümde desteklenmiyor (emülasyon sağlanmaz).";
            return;
        }

        if (_settings.Current.ConfirmBeforeLaunch &&
            !_dialogs.Confirm("Başlat", $"{target.Title}\n\n{Path.GetFileName(target.FilePath)}"))
        {
            return;
        }

        await RunBusyAsync(async ct =>
        {
            // Kullanıcı uğraşmasın: emülatör/BIOS yolu eksikse başlamadan önce kendimiz ararız.
            await _autoSetup.EnsurePlatformAsync(target.Model.PlatformId, ct).ConfigureAwait(false);

            var validation = await _launchService.ValidateAsync(target.Model, ct).ConfigureAwait(false);

            if (validation.HasErrors)
            {
                await Dispatcher.InvokeAsync(() =>
                    _dialogs.ShowMessage("Başlatılamıyor", validation.Summary));
                return;
            }

            var outcome = await _launchService.LaunchAsync(target.Model, ct).ConfigureAwait(false);

            StatusMessage = outcome.Success
                ? $"{target.Title} başlatıldı (PID {outcome.ProcessId})."
                : $"Başlatılamadı: {outcome.Message}";

            if (!outcome.Success)
            {
                await Dispatcher.InvokeAsync(() => _dialogs.ShowMessage("Başlatılamadı", outcome.Message ?? "Bilinmeyen hata"));
            }
            else if (_settings.Current.MinimizeOnLaunch)
            {
                Logger.Info(nameof(LibraryViewModel), "MinimizeOnLaunch ayarı açık (UI katmanı uygular).");
            }
        }, "Başlatılıyor...");
    }

    [RelayCommand]
    private void ShowDetails(GameItemViewModel? item)
    {
        var target = item ?? SelectedGame;
        if (target is not null)
        {
            DetailsRequested?.Invoke(this, target.Model);
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(GameItemViewModel? item)
    {
        var target = item ?? SelectedGame;
        if (target is null)
        {
            return;
        }

        target.IsFavorite = !target.IsFavorite;

        try
        {
            await _repository.UpsertGameAsync(target.Model).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(nameof(LibraryViewModel), "Favori kaydedilemedi.", ex);
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(GameItemViewModel? item)
    {
        var target = item ?? SelectedGame;
        if (target is null)
        {
            return;
        }

        // Yalnızca kütüphane kaydı silinir; kullanıcının dosyasına dokunulmaz.
        if (!_dialogs.Confirm("Kaldır", $"{target.Title} kütüphaneden çıkarılsın mı?\n(Dosyalar silinmez.)"))
        {
            return;
        }

        await RunBusyAsync(async ct =>
        {
            await _repository.DeleteGameAsync(target.Id, ct).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => Games.Remove(target));
            VisibleCount = Games.Count;
            StatusMessage = $"{target.Title} kaldırıldı.";
        });
    }

    [RelayCommand]
    private void OpenFolder(GameItemViewModel? item)
    {
        var target = item ?? SelectedGame;
        if (target is not null)
        {
            _shell.RevealInExplorer(target.FilePath);
        }
    }

    /// <summary>Enter / durum çubuğu "Başlat" butonu için parametresiz sürüm.</summary>
    [RelayCommand]
    private Task LaunchSelectedAsync() => LaunchAsync(SelectedGame);

    [RelayCommand]
    private void ShowDetailsSelected()
        => ShowDetails(SelectedGame);

    [RelayCommand]
    private Task ToggleFavoriteSelectedAsync() => ToggleFavoriteAsync(SelectedGame);

    [RelayCommand]
    private void SelectPlatform(PlatformFilterItem? item)
        => SelectedPlatform = item;

    [RelayCommand]
    private void SetGrid()
        => ViewMode = LibraryViewMode.Grid;

    [RelayCommand]
    private void SetList()
        => ViewMode = LibraryViewMode.List;

    [RelayCommand]
    private void OpenSettings()
        => SettingsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task RescanAsync()
    {
        _scanCts = new CancellationTokenSource();
        ScanProgress = 0;

        await RunBusyAsync(async ct =>
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                _ = Dispatcher.InvokeAsync(() => ScanProgress = p.Found == 0 ? 0 : (double)p.Processed / p.Found);
            });

            var result = await _maintenance.RescanAsync(progress, ct).ConfigureAwait(false);

            StatusMessage = result.ToString();
            await LoadPlatformFiltersAsync(ct).ConfigureAwait(false);
            await LoadGamesAsync(ct).ConfigureAwait(false);

            if (result.NeedsReview > 0)
            {
                await Dispatcher.InvokeAsync(() => _dialogs.ShowMessage(
                    "Platformu doğrula",
                    $"{result.NeedsReview} kaydın platformu belirlenemedi. Kart üzerinden platformu elle seçebilirsin."));
            }
        }, "Kütüphane taranıyor...", _scanCts.Token);
    }

    [RelayCommand]
    private void CancelScan()
    {
        _scanCts?.Cancel();
        StatusMessage = "Tarama iptal ediliyor...";
    }

    [RelayCommand]
    private void ToggleViewMode()
        => ViewMode = ViewMode == LibraryViewMode.Grid ? LibraryViewMode.List : LibraryViewMode.Grid;

    [RelayCommand]
    private void ClearSearch()
        => SearchText = string.Empty;

    // ------------------------------------------------------- property tetikleyicileri

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token).ConfigureAwait(false);
                await LoadGamesAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // kullanıcı yazmaya devam ediyor
            }
        }, token);
    }

    partial void OnSelectedPlatformChanged(PlatformFilterItem? value)
        => _ = RunBusyAsync(LoadGamesAsync);

    partial void OnFavoritesOnlyChanged(bool value)
        => _ = RunBusyAsync(LoadGamesAsync);

    partial void OnSortByChanged(GameSort value)
    {
        _settings.Current.SortBy = value;
        _ = _settings.SaveAsync();
        _ = RunBusyAsync(LoadGamesAsync);
    }

    partial void OnViewModeChanged(LibraryViewMode value)
    {
        OnPropertyChanged(nameof(IsGridView));
        OnPropertyChanged(nameof(ViewModeText));
        _settings.Current.LibraryViewMode = value;
        _ = _settings.SaveAsync();
    }

    // ---------------------------------------------------------- oyun klasörü

    public string GamesDirectory => EmulatorSetupService.GamesDirectory;

    /// <summary>Kullanıcının kendi oyun klasörünü Explorer'da açar (yoksa oluşturur).</summary>
    [RelayCommand]
    private void OpenGamesFolder()
    {
        EmulatorSetupService.EnsureGamesDirectory();
        _shell.OpenFolder(EmulatorSetupService.GamesDirectory);
    }
}
