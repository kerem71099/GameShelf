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

public sealed partial class EmulatorSettingItem : ObservableObject
{
    public PlatformId PlatformId { get; init; }

    public string PlatformName { get; init; } = string.Empty;

    public string BackendName { get; init; } = string.Empty;

    public bool IsSupported { get; init; }

    public string BiosHint { get; init; } = string.Empty;

    /// <summary>"Varsayılana sıfırla" için backend'in önerdiği şablon.</summary>
    public string DefaultTemplate { get; init; } = string.Empty;

    [ObservableProperty] private string? _executablePath;
    [ObservableProperty] private string? _workingDirectory;
    [ObservableProperty] private string? _argumentTemplate;
    [ObservableProperty] private bool _defaultFullscreen = true;
    [ObservableProperty] private string? _extraArguments;

    public EmulatorConfig ToConfig(Guid id) => new()
    {
        Id = id,
        PlatformId = PlatformId,
        Name = BackendName,
        ExecutablePath = string.IsNullOrWhiteSpace(ExecutablePath) ? null : ExecutablePath,
        WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory,
        ArgumentTemplate = string.IsNullOrWhiteSpace(ArgumentTemplate) ? null : ArgumentTemplate,
        DefaultFullscreen = DefaultFullscreen,
        ExtraArguments = string.IsNullOrWhiteSpace(ExtraArguments) ? null : ExtraArguments,
        UpdatedAt = DateTimeOffset.Now
    };
}

public sealed partial class BiosPathItem : ObservableObject
{
    public PlatformId PlatformId { get; init; }

    public string PlatformName { get; init; } = string.Empty;

    [ObservableProperty] private string? _path;
}

/// <summary>Ayarlar ekranı: kütüphane klasörleri, emülatörler, görünüm, gelişmiş.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ILibraryRepository _repository;
    private readonly ISettingsService _settings;
    private readonly IEmulatorBackendFactory _backends;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;

    private readonly Dictionary<PlatformId, Guid> _emulatorConfigIds = new();

    public SettingsViewModel(
        ILibraryRepository repository,
        ISettingsService settings,
        IEmulatorBackendFactory backends,
        IDialogService dialogs,
        IShellService shell,
        ILoggingService logger,
        IDispatcher dispatcher)
        : base(logger, dispatcher)
    {
        _repository = repository;
        _settings = settings;
        _backends = backends;
        _dialogs = dialogs;
        _shell = shell;

        Themes = new ObservableCollection<string> { "Dark", "Light", "System" };
        EmulatorFilter = "Uygulama|*.exe|Tüm dosyalar|*.*";
    }

    public ObservableCollection<LibraryFolder> Folders { get; } = new();

    public ObservableCollection<EmulatorSettingItem> Emulators { get; } = new();

    public ObservableCollection<BiosPathItem> BiosPaths { get; } = new();

    public ObservableCollection<string> Themes { get; }

    public string EmulatorFilter { get; }

    public string LegalNote => BiosCheckService.Notice;

    /// <summary>Hangi derlemenin çalıştığı (destek sırasında sürüm karışmasın).</summary>
    public string AppVersion => BuildStamp.Display;

    [ObservableProperty] private LibraryFolder? _selectedFolder;
    [ObservableProperty] private string _theme = "Dark";
    [ObservableProperty] private bool _confirmBeforeLaunch = true;
    [ObservableProperty] private bool _minimizeOnLaunch = true;
    [ObservableProperty] private bool _showUnsupportedPlatforms = true;
    [ObservableProperty] private string _databasePath = string.Empty;
    [ObservableProperty] private string _metadataPath = string.Empty;
    [ObservableProperty] private string _extraScanExtensions = string.Empty;

    // ------------------------------------------------------------------ yükleme

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunBusyAsync(async ct =>
        {
            var settings = _settings.Current;
            Theme = settings.Theme;
            ConfirmBeforeLaunch = settings.ConfirmBeforeLaunch;
            MinimizeOnLaunch = settings.MinimizeOnLaunch;
            ShowUnsupportedPlatforms = settings.ShowUnsupportedPlatforms;
            DatabasePath = settings.DatabasePath;
            MetadataPath = settings.MetadataPath;
            ExtraScanExtensions = string.Join(", ", settings.ExtraScanExtensions);

            var folders = await _repository.GetLibraryFoldersAsync(ct).ConfigureAwait(false);
            var platforms = await _repository.GetPlatformsAsync(ct).ConfigureAwait(false);
            var configs = await _repository.GetEmulatorConfigsAsync(ct).ConfigureAwait(false);

            var emulatorItems = new List<EmulatorSettingItem>();
            var biosItems = new List<BiosPathItem>();

            foreach (var platform in platforms
                         .Where(p => p.Id is PlatformId.Ps1 or PlatformId.Ps2 or PlatformId.Ps3)
                         .OrderBy(p => p.SortOrder))
            {
                var backend = _backends.Get(platform.Id);
                var config = configs.FirstOrDefault(c => c.PlatformId == platform.Id);

                if (config is not null)
                {
                    _emulatorConfigIds[platform.Id] = config.Id;
                }

                emulatorItems.Add(new EmulatorSettingItem
                {
                    PlatformId = platform.Id,
                    PlatformName = platform.ShortName,
                    BackendName = backend?.DisplayName ?? platform.Name,
                    IsSupported = platform.IsSupported,
                    BiosHint = backend?.BiosHint ?? string.Empty,
                    DefaultTemplate = backend?.DefaultArgumentTemplate ?? string.Empty,
                    ExecutablePath = config?.ExecutablePath,
                    WorkingDirectory = config?.WorkingDirectory,
                    ArgumentTemplate = config?.ArgumentTemplate,
                    DefaultFullscreen = config?.DefaultFullscreen ?? true,
                    ExtraArguments = config?.ExtraArguments
                });

                settings.BiosPaths.TryGetValue(platform.Id.ToString(), out var biosPath);
                biosItems.Add(new BiosPathItem
                {
                    PlatformId = platform.Id,
                    PlatformName = platform.ShortName,
                    Path = biosPath
                });
            }

            await Dispatcher.InvokeAsync(() =>
            {
                Folders.Clear();
                foreach (var folder in folders)
                {
                    Folders.Add(folder);
                }

                Emulators.Clear();
                foreach (var item in emulatorItems)
                {
                    Emulators.Add(item);
                }

                BiosPaths.Clear();
                foreach (var item in biosItems)
                {
                    BiosPaths.Add(item);
                }
            });
        }, "Ayarlar yükleniyor...");
    }

    // ------------------------------------------------------------------ kaydetme

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunBusyAsync(async ct =>
        {
            await _settings.UpdateAsync(settings =>
            {
                settings.Theme = Theme;
                settings.ConfirmBeforeLaunch = ConfirmBeforeLaunch;
                settings.MinimizeOnLaunch = MinimizeOnLaunch;
                settings.ShowUnsupportedPlatforms = ShowUnsupportedPlatforms;
                settings.ExtraScanExtensions = ExtraScanExtensions
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(e => e.StartsWith('.'))
                    .ToList();

                foreach (var bios in BiosPaths)
                {
                    if (string.IsNullOrWhiteSpace(bios.Path))
                    {
                        settings.BiosPaths.Remove(bios.PlatformId.ToString());
                    }
                    else
                    {
                        settings.BiosPaths[bios.PlatformId.ToString()] = bios.Path;
                    }
                }
            }, ct).ConfigureAwait(false);

            foreach (var folder in Folders)
            {
                await _repository.UpsertLibraryFolderAsync(folder, ct).ConfigureAwait(false);
            }

            foreach (var emulator in Emulators)
            {
                var id = _emulatorConfigIds.TryGetValue(emulator.PlatformId, out var existing)
                    ? existing
                    : Guid.NewGuid();

                await _repository.UpsertEmulatorConfigAsync(emulator.ToConfig(id), ct).ConfigureAwait(false);
            }

            StatusMessage = "Ayarlar kaydedildi.";
        }, "Kaydediliyor...");
    }

    // ------------------------------------------------------------------ klasörler

    [RelayCommand]
    private void AddFolder()
    {
        var path = _dialogs.OpenFolder("Oyun klasörü seç (yasal olarak sahip olduğun dump'lar)");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (Folders.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "Bu klasör zaten ekli.";
            return;
        }

        Folders.Add(new LibraryFolder { Path = path, Recursive = true, IsEnabled = true });
    }

    [RelayCommand]
    private async Task RemoveFolderAsync(LibraryFolder? folder)
    {
        var target = folder ?? SelectedFolder;
        if (target is null)
        {
            return;
        }

        if (!_dialogs.Confirm("Kaldır", $"{target.Path}\n\nKütüphane klasörü kaldırılsın mı? (Oyun dosyaları silinmez.)"))
        {
            return;
        }

        await _repository.DeleteLibraryFolderAsync(target.Id).ConfigureAwait(false);
        await Dispatcher.InvokeAsync(() => Folders.Remove(target));
    }

    // ------------------------------------------------------------------ emülatörler

    [RelayCommand]
    private void BrowseEmulator(EmulatorSettingItem? item)
    {
        if (item is null)
        {
            return;
        }

        var file = _dialogs.OpenFile($"{item.PlatformName} emülatörünü seç", EmulatorFilter);
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }

        item.ExecutablePath = file;

        // Çalışma dizini boşsa exe'nin klasörünü öner.
        if (string.IsNullOrWhiteSpace(item.WorkingDirectory))
        {
            item.WorkingDirectory = Path.GetDirectoryName(file);
        }
    }

    [RelayCommand]
    private void ResetTemplate(EmulatorSettingItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.ArgumentTemplate = item.DefaultTemplate;
        StatusMessage = $"{item.PlatformName} argüman şablonu varsayılana döndü.";
    }

    [RelayCommand]
    private void BrowseBios(BiosPathItem? item)
    {
        if (item is null)
        {
            return;
        }

        var folder = _dialogs.OpenFolder($"{item.PlatformName} BIOS/firmware klasörü (kendi dump'ın)");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            item.Path = folder;
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
        => _shell.OpenFolder(Path.GetDirectoryName(_settings.Current.DatabasePath) ?? ".");
}
