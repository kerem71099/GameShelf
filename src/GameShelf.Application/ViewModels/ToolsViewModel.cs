using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Application.Services;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;

namespace GameShelf.Application.ViewModels;

public sealed partial class BiosStatusRow : ObservableObject
{
    public PlatformId PlatformId { get; init; }

    public string PlatformName { get; init; } = string.Empty;

    public bool Configured { get; init; }

    public bool Exists { get; init; }

    public string? Path { get; init; }

    public string Hint { get; init; } = string.Empty;

    public string StatusText => !Configured ? "Yapılandırılmadı" : Exists ? "Bulundu" : "Yol geçersiz";

    public string StatusBrushKey => !Configured ? "Brush.TextMuted" : Exists ? "Brush.Success" : "Brush.Warning";
}

/// <summary>Tools ekranı: BIOS kontrolü, kayıp dosyalar, içe aktarılmamış dosyalar, yeniden tarama, log.</summary>
public sealed partial class ToolsViewModel : ViewModelBase
{
    private readonly ILibraryRepository _repository;
    private readonly LibraryMaintenanceService _maintenance;
    private readonly BiosCheckService _biosCheck;
    private readonly ISettingsService _settings;
    private readonly IShellService _shell;
    private readonly IDialogService _dialogs;

    private CancellationTokenSource? _scanCts;

    public ToolsViewModel(
        ILibraryRepository repository,
        LibraryMaintenanceService maintenance,
        BiosCheckService biosCheck,
        ISettingsService settings,
        IShellService shell,
        IDialogService dialogs,
        ILoggingService logger,
        IDispatcher dispatcher)
        : base(logger, dispatcher)
    {
        _repository = repository;
        _maintenance = maintenance;
        _biosCheck = biosCheck;
        _settings = settings;
        _shell = shell;
        _dialogs = dialogs;
    }

    public ObservableCollection<BiosStatusRow> BiosRows { get; } = new();

    public ObservableCollection<GameItemViewModel> MissingGames { get; } = new();

    public ObservableCollection<string> UnimportedFiles { get; } = new();

    public ObservableCollection<LaunchHistoryEntry> History { get; } = new();

    public string LegalNote => BiosCheckService.Notice;

    public string LogDirectory => _settings.Current.LogDirectory;

    [ObservableProperty] private string _output = string.Empty;

    [ObservableProperty] private double _progress;

    // ------------------------------------------------------------------ komutlar

    [RelayCommand]
    private async Task RunBiosCheckAsync()
    {
        await RunBusyAsync(async ct =>
        {
            var statuses = await _biosCheck.CheckAsync(ct).ConfigureAwait(false);

            await Dispatcher.InvokeAsync(() =>
            {
                BiosRows.Clear();

                foreach (var status in statuses)
                {
                    BiosRows.Add(new BiosStatusRow
                    {
                        PlatformId = status.PlatformId,
                        PlatformName = status.PlatformName,
                        Configured = status.Configured,
                        Exists = status.Exists,
                        Path = status.Path,
                        Hint = status.Hint
                    });
                }
            });

            Output = string.Join(Environment.NewLine, statuses.Select(s =>
                $"{s.PlatformName}: {(s.Configured ? (s.Exists ? "bulundu" : "yol geçersiz") : "yapılandırılmadı")}"));
        }, "BIOS yolları kontrol ediliyor...");
    }

    [RelayCommand]
    private async Task FindMissingAsync()
    {
        await RunBusyAsync(async ct =>
        {
            var missing = await _maintenance.FindMissingGamesAsync(ct).ConfigureAwait(false);

            await Dispatcher.InvokeAsync(() =>
            {
                MissingGames.Clear();
                foreach (var game in missing)
                {
                    MissingGames.Add(new GameItemViewModel(game, Logger, Dispatcher));
                }
            });

            Output = $"{missing.Count} kayıt diskte bulunamadı.";
        }, "Kayıp dosyalar aranıyor...");
    }

    [RelayCommand]
    private async Task RemoveMissingAsync()
    {
        var ids = MissingGames.Select(g => g.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        if (!_dialogs.Confirm("Kaldır", $"{ids.Count} kayıt kütüphaneden çıkarılsın mı? (Dosyalar silinmez.)"))
        {
            return;
        }

        await RunBusyAsync(async ct =>
        {
            await _maintenance.RemoveGamesAsync(ids, ct).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => MissingGames.Clear());
            Output = $"{ids.Count} kayıt kaldırıldı.";
        });
    }

    [RelayCommand]
    private async Task FindUnimportedAsync()
    {
        await RunBusyAsync(async ct =>
        {
            var files = await _maintenance.FindUnimportedFilesAsync(ct).ConfigureAwait(false);

            await Dispatcher.InvokeAsync(() =>
            {
                UnimportedFiles.Clear();
                foreach (var file in files)
                {
                    UnimportedFiles.Add(file);
                }
            });

            Output = $"{files.Count} dosya kütüphanede değil.";
        }, "İçe aktarılmamış dosyalar aranıyor...");
    }

    [RelayCommand]
    private async Task RescanAsync()
    {
        _scanCts = new CancellationTokenSource();
        Progress = 0;

        await RunBusyAsync(async ct =>
        {
            var progress = new Progress<ScanProgress>(p =>
                _ = Dispatcher.InvokeAsync(() =>
                    Progress = p.Found == 0 ? 0 : (double)p.Processed / p.Found));

            var result = await _maintenance.RescanAsync(progress, ct).ConfigureAwait(false);
            Output = result.ToString();
        }, "Kütüphane taranıyor...", _scanCts.Token);
    }

    [RelayCommand]
    private void CancelScan()
        => _scanCts?.Cancel();

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        await RunBusyAsync(async ct =>
        {
            var history = await _repository.GetLaunchHistoryAsync(200, ct).ConfigureAwait(false);

            await Dispatcher.InvokeAsync(() =>
            {
                History.Clear();
                foreach (var entry in history)
                {
                    History.Add(entry);
                }
            });

            Output = $"Son {history.Count} başlatma kaydı.";
        }, "Launch log yükleniyor...");
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (!_dialogs.Confirm("Temizle", "Launch log temizlensin mi?"))
        {
            return;
        }

        await RunBusyAsync(async ct =>
        {
            await _repository.ClearLaunchHistoryAsync(ct).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => History.Clear());
            Output = "Launch log temizlendi.";
        });
    }

    [RelayCommand]
    private void OpenLogs()
        => _shell.OpenFolder(_settings.Current.LogDirectory);

    [RelayCommand]
    private void OpenDataFolder()
        => _shell.OpenFolder(Path.GetDirectoryName(_settings.Current.DatabasePath) ?? ".");
}
