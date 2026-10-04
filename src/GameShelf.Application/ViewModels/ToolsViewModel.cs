using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Application.Services;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Interfaces;

namespace GameShelf.Application.ViewModels;

public sealed partial class BiosStatusRow : ObservableObject
{
    public PlatformId PlatformId { get; init; }

    public string PlatformName { get; init; } = string.Empty;

    public bool Configured { get; init; }

    public bool Exists { get; init; }

    public string? Path { get; init; }

    /// <summary>BIOS'un konması gereken klasör (emülatörün kendi klasörü).</summary>
    public string? TargetDirectory { get; init; }

    public string Hint { get; init; } = string.Empty;

    public bool HasTargetDirectory => !string.IsNullOrWhiteSpace(TargetDirectory);

    public string StatusText => !Configured ? "Yapılandırılmadı" : Exists ? "Bulundu" : "Yol geçersiz";

    public string StatusBrushKey => !Configured ? "Brush.TextMuted" : Exists ? "Brush.Success" : "Brush.Warning";
}

/// <summary>Araçlar ekranındaki emülatör kurulum satırı.</summary>
public sealed partial class EmulatorSetupRow : ObservableObject
{
    public string PlatformKey { get; init; } = string.Empty;

    public string PlatformName { get; init; } = string.Empty;

    public string EmulatorName { get; init; } = string.Empty;

    public string Hint { get; init; } = string.Empty;

    [ObservableProperty] private string _exePath = string.Empty;

    [ObservableProperty] private string _statusText = string.Empty;

    [ObservableProperty] private string _statusBrushKey = "Brush.TextMuted";
}

/// <summary>Tools ekranı: BIOS kontrolü, kayıp dosyalar, içe aktarılmamış dosyalar, yeniden tarama, log.</summary>
public sealed partial class ToolsViewModel : ViewModelBase
{
    private readonly ILibraryRepository _repository;
    private readonly LibraryMaintenanceService _maintenance;
    private readonly BiosCheckService _biosCheck;
    private readonly EmulatorSetupService _emulatorSetup;
    private readonly IEmulatorBackendFactory _backends;
    private readonly ISettingsService _settings;
    private readonly IShellService _shell;
    private readonly IDialogService _dialogs;
    private readonly EmulatorAutoSetupService _autoSetup;

    private CancellationTokenSource? _scanCts;

    public ToolsViewModel(
        ILibraryRepository repository,
        LibraryMaintenanceService maintenance,
        BiosCheckService biosCheck,
        EmulatorSetupService emulatorSetup,
        IEmulatorBackendFactory backends,
        ISettingsService settings,
        IShellService shell,
        IDialogService dialogs,
        EmulatorAutoSetupService autoSetup,
        ILoggingService logger,
        IDispatcher dispatcher)
        : base(logger, dispatcher)
    {
        _repository = repository;
        _maintenance = maintenance;
        _biosCheck = biosCheck;
        _emulatorSetup = emulatorSetup;
        _backends = backends;
        _settings = settings;
        _shell = shell;
        _dialogs = dialogs;
        _autoSetup = autoSetup;
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
        => await RunBusyAsync(LoadBiosRowsAsync, "BIOS yolları kontrol ediliyor...");

    private async Task LoadBiosRowsAsync(CancellationToken ct)
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
                    TargetDirectory = status.TargetDirectory,
                    Hint = status.Hint
                });
            }
        });

        Output = string.Join(Environment.NewLine, statuses.Select(s =>
            $"{s.PlatformName}: {(s.Configured ? (s.Exists ? "bulundu" : "yol geçersiz") : "yapılandırılmadı")}"));
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

    // ---------------------------------------------------------- kurulum / eksikler

    public ObservableCollection<EmulatorSetupRow> SetupRows { get; } = new();

    public string SetupNote => EmulatorSetupService.Notice;

    public string GamesDirectory => EmulatorSetupService.GamesDirectory;

    /// <summary>Son otomatik kurulumun özeti (bulunan emülatör/BIOS yolları).</summary>
    [ObservableProperty] private string _setupStatus = string.Empty;

    /// <summary>
    /// Tek düğmeyle her şeyi kendimiz buluruz: emülatör exe'leri + BIOS/firmware klasörleri.
    /// Hiçbir dosya indirilmez; diskte zaten kurulu olanların yolu kaydedilir.
    /// </summary>
    [RelayCommand]
    private async Task AutoSetupAsync()
    {
        await RunBusyAsync(async ct =>
        {
            var report = await _autoSetup.RunForcedAsync(ct).ConfigureAwait(false);

            await LoadSetupRowsAsync(ct).ConfigureAwait(false);
            await LoadBiosRowsAsync(ct).ConfigureAwait(false);

            SetupStatus = report.Summary;
            Output = report.Summary;
        }, "Otomatik kurulum: emülatörler ve BIOS klasörleri aranıyor...");
    }

    [RelayCommand]
    private async Task RefreshSetupAsync()
        => await RunBusyAsync(LoadSetupRowsAsync, "Kurulum durumu okunuyor...");

    private async Task LoadSetupRowsAsync(CancellationToken ct)
    {
        var configs = await _repository.GetEmulatorConfigsAsync(ct).ConfigureAwait(false);
        var rows = new List<EmulatorSetupRow>();

        foreach (var info in _emulatorSetup.Downloads)
        {
            if (!TryParsePlatform(info.PlatformKey, out var platform))
            {
                continue;
            }

            var config = configs.FirstOrDefault(c => c.PlatformId == platform);
            var configured = config is not null && !string.IsNullOrWhiteSpace(config.ExecutablePath);
            var exists = configured && File.Exists(config!.ExecutablePath!);

            rows.Add(new EmulatorSetupRow
            {
                PlatformKey = info.PlatformKey,
                PlatformName = info.PlatformName,
                EmulatorName = info.EmulatorName,
                Hint = info.Hint,
                ExePath = config?.ExecutablePath ?? string.Empty,
                StatusText = !configured ? "Kurulu değil" : exists ? "Hazır" : "Yol geçersiz",
                StatusBrushKey = !configured ? "Brush.TextMuted" : exists ? "Brush.Success" : "Brush.Warning"
            });
        }

        await Dispatcher.InvokeAsync(() =>
        {
            SetupRows.Clear();

            foreach (var row in rows)
            {
                SetupRows.Add(row);
            }
        });
    }

    /// <summary>
    /// BIOS'un konması gereken klasörü açar (gerekirse oluşturur).
    /// Kullanıcı kendi BIOS'unu sürükleyip bıraksın diye; içine hiçbir şey yazmayız.
    /// </summary>
    [RelayCommand]
    private void OpenBiosFolder(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            _shell.OpenFolder(directory);
            Output = $"Klasör açıldı: {directory}";
        }
        catch (Exception ex)
        {
            Logger.Error(nameof(ToolsViewModel), "BIOS klasörü açılamadı.", ex);
            Output = $"Klasör açılamadı: {directory}";
        }
    }

    /// <summary>Emülatörün resmî indirme sayfasını tarayıcıda açar (ikili dosya dağıtmayız).</summary>
    [RelayCommand]
    private void OpenEmulatorDownload(string platformKey)
        => _emulatorSetup.OpenOfficialPage(platformKey);

    /// <summary>Bilinen konumlarda kurulu emülatörü arar ve kaydeder.</summary>
    [RelayCommand]
    private async Task AutoDetectEmulatorAsync(string platformKey)
    {
        if (!TryParsePlatform(platformKey, out var platform))
        {
            return;
        }

        var found = await Task.Run(() => _emulatorSetup.DetectExecutable(platformKey)).ConfigureAwait(false);

        if (found is null)
        {
            Output = $"{platform}: bilinen konumlarda bulunamadı. 'İndir' ile resmî sayfayı açıp kurun, sonra 'Gözat' ile exe'yi seçin.";
            return;
        }

        await SaveEmulatorPathAsync(platform, found).ConfigureAwait(false);
        await RefreshSetupAsync().ConfigureAwait(false);
        Output = $"{platform} emülatörü bulundu: {found}";
    }

    /// <summary>Kullanıcı kendi emülatör exe'sini seçer.</summary>
    [RelayCommand]
    private async Task BrowseEmulatorAsync(string platformKey)
    {
        if (!TryParsePlatform(platformKey, out var platform))
        {
            return;
        }

        var file = _dialogs.OpenFile($"{platform} emülatörünü seç", "Uygulama|*.exe|Tüm dosyalar|*.*");

        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }

        await SaveEmulatorPathAsync(platform, file).ConfigureAwait(false);
        await RefreshSetupAsync().ConfigureAwait(false);
        Output = $"{platform} emülatörü kaydedildi: {file}";
    }

    [RelayCommand]
    private void OpenGamesFolder()
    {
        EmulatorSetupService.EnsureGamesDirectory();
        _shell.OpenFolder(EmulatorSetupService.GamesDirectory);
    }

    // ---------------------------------------------------------- yardımcılar

    private static bool TryParsePlatform(string? key, out PlatformId platform)
    {
        platform = default;

        return !string.IsNullOrWhiteSpace(key)
               && Enum.TryParse(key, ignoreCase: true, out platform)
               && platform is PlatformId.Ps1 or PlatformId.Ps2 or PlatformId.Ps3;
    }

    private async Task SaveEmulatorPathAsync(PlatformId platform, string exePath)
    {
        var configs = await _repository.GetEmulatorConfigsAsync(CancellationToken.None).ConfigureAwait(false);
        var existing = configs.FirstOrDefault(c => c.PlatformId == platform);
        var backend = _backends.Get(platform);

        var config = new EmulatorConfig
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            PlatformId = platform,
            Name = existing?.Name ?? backend?.DisplayName ?? platform.ToString(),
            ExecutablePath = exePath,
            WorkingDirectory = existing?.WorkingDirectory ?? Path.GetDirectoryName(exePath),
            ArgumentTemplate = existing?.ArgumentTemplate ?? backend?.DefaultArgumentTemplate,
            DefaultFullscreen = existing?.DefaultFullscreen ?? true,
            ExtraArguments = existing?.ExtraArguments,
            UpdatedAt = DateTimeOffset.Now
        };

        await _repository.UpsertEmulatorConfigAsync(config, CancellationToken.None).ConfigureAwait(false);
    }
}
