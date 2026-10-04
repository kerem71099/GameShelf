using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameShelf.Application.Abstractions;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Extensions;

namespace GameShelf.Application.ViewModels;

public enum FullscreenMode
{
    /// <summary>Emülatörün/platformun varsayılanı.</summary>
    Default = 0,

    On = 1,
    Off = 2
}

/// <summary>Oyun detay ekranı: kapak, metadata düzenleme, başlatma ve oyun bazlı override.</summary>
public sealed partial class GameDetailsViewModel : ViewModelBase
{
    private readonly ILibraryRepository _repository;
    private readonly IEmulatorLaunchService _launchService;
    private readonly ICoverImageService _covers;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;

    private Game _game = new();

    public GameDetailsViewModel(
        ILibraryRepository repository,
        IEmulatorLaunchService launchService,
        ICoverImageService covers,
        ISettingsService settings,
        IDialogService dialogs,
        IShellService shell,
        ILoggingService logger,
        IDispatcher dispatcher)
        : base(logger, dispatcher)
    {
        _repository = repository;
        _launchService = launchService;
        _covers = covers;
        _settings = settings;
        _dialogs = dialogs;
        _shell = shell;

        Platforms =
        [
            PlatformId.Ps1,
            PlatformId.Ps2,
            PlatformId.Ps3,
            PlatformId.Unknown
        ];
    }

    public ObservableCollection<PlatformId> Platforms { get; }

    /// <summary>Kütüphaneye dönüş isteği (MainViewModel dinler).</summary>
    public event EventHandler? BackRequested;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _region;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _coverPath;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private PlatformId _platformId = PlatformId.Unknown;

    [ObservableProperty]
    private FullscreenMode _fullscreenMode = FullscreenMode.Default;

    [ObservableProperty]
    private string? _extraArguments;

    [ObservableProperty]
    private string? _argumentTemplate;

    [ObservableProperty]
    private string? _commandPreview;

    public Game Model => _game;

    public string FilePath => _game.FilePath;

    public string PlatformShort => _game.PlatformId.ToShortName();

    public string PlayTimeText => _game.PlayTimeMinutes switch
    {
        0 => "—",
        < 60 => $"{_game.PlayTimeMinutes} dk",
        _ => $"{_game.PlayTimeMinutes / 60}s {_game.PlayTimeMinutes % 60}dk"
    };

    public string LastPlayedText => _game.LastPlayedAt is null
        ? "Hiç oynanmadı"
        : _game.LastPlayedAt.Value.LocalDateTime.ToString("dd.MM.yyyy HH:mm");

    public void Load(Game game)
    {
        _game = game;

        Title = game.Title;
        Region = game.Region;
        Notes = game.Notes;
        CoverPath = game.CoverPath;
        IsFavorite = game.IsFavorite;
        PlatformId = game.PlatformId;

        FullscreenMode = FullscreenMode.Default;
        ExtraArguments = null;
        ArgumentTemplate = null;
        CommandPreview = null;

        _ = LoadOverrideAsync();
    }

    private async Task LoadOverrideAsync()
    {
        try
        {
            var gameOverride = await _repository.GetGameOverrideAsync(_game.Id).ConfigureAwait(false);

            if (gameOverride is not null)
            {
                FullscreenMode = gameOverride.Fullscreen switch
                {
                    true => FullscreenMode.On,
                    false => FullscreenMode.Off,
                    null => FullscreenMode.Default
                };

                ExtraArguments = gameOverride.ExtraArguments;
                ArgumentTemplate = gameOverride.ArgumentTemplate;
            }

            await RefreshPreviewAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(nameof(GameDetailsViewModel), "Override yüklenemedi.", ex);
        }
    }

    // ------------------------------------------------------------------ komutlar

    [RelayCommand]
    private async Task LaunchAsync()
    {
        await SaveAsync().ConfigureAwait(false);

        await RunBusyAsync(async ct =>
        {
            var validation = await _launchService.ValidateAsync(_game, ct).ConfigureAwait(false);

            if (validation.HasErrors)
            {
                await Dispatcher.InvokeAsync(() => _dialogs.ShowMessage("Başlatılamıyor", validation.Summary));
                return;
            }

            var outcome = await _launchService.LaunchAsync(_game, ct).ConfigureAwait(false);
            StatusMessage = outcome.Success ? "Başlatıldı." : $"Başlatılamadı: {outcome.Message}";
        }, "Başlatılıyor...");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            _game.Title = Title;
            _game.SortTitle = Services.TitleCleaner.ToSortTitle(Title);
            _game.Region = Region;
            _game.Notes = Notes;
            _game.CoverPath = CoverPath;
            _game.IsFavorite = IsFavorite;
            _game.PlatformId = PlatformId;

            await _repository.UpsertGameAsync(_game).ConfigureAwait(false);

            if (FullscreenMode == FullscreenMode.Default
                && string.IsNullOrWhiteSpace(ExtraArguments)
                && string.IsNullOrWhiteSpace(ArgumentTemplate))
            {
                await _repository.DeleteGameOverrideAsync(_game.Id).ConfigureAwait(false);
            }
            else
            {
                await _repository.UpsertGameOverrideAsync(new GameOverride
                {
                    GameId = _game.Id,
                    Fullscreen = FullscreenMode switch
                    {
                        FullscreenMode.On => true,
                        FullscreenMode.Off => false,
                        _ => null
                    },
                    ExtraArguments = string.IsNullOrWhiteSpace(ExtraArguments) ? null : ExtraArguments,
                    ArgumentTemplate = string.IsNullOrWhiteSpace(ArgumentTemplate) ? null : ArgumentTemplate,
                    UpdatedAt = DateTimeOffset.Now
                }).ConfigureAwait(false);
            }

            StatusMessage = "Kaydedildi.";
            await RefreshPreviewAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(nameof(GameDetailsViewModel), "Kayıt başarısız.", ex);
            ErrorMessage = "Kaydedilemedi: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task PickCoverAsync()
    {
        // Offline: kaynak her zaman kullanıcının seçtiği yerel dosyadır.
        var file = _dialogs.OpenFile(
            "Kapak seç",
            "Resim dosyaları|*.png;*.jpg;*.jpeg|Tüm dosyalar|*.*",
            Path.GetDirectoryName(_game.FilePath));

        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }

        var imported = await _covers.ImportAsync(_game.Id, file).ConfigureAwait(false);

        if (imported is null)
        {
            ErrorMessage = "Kapak dosyası okunamadı (PNG/JPEG olmalı).";
            return;
        }

        CoverPath = imported;
        await SaveAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private void ToggleFavorite()
        => IsFavorite = !IsFavorite;

    [RelayCommand]
    private void OpenFolder()
        => _shell.RevealInExplorer(_game.FilePath);

    [RelayCommand]
    private void Back()
        => BackRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task ResetTemplateAsync()
    {
        ArgumentTemplate = null;
        await RefreshPreviewAsync().ConfigureAwait(false);
        StatusMessage = "Şablon sıfırlandı (emülatör varsayılanı kullanılacak).";
    }

    private async Task RefreshPreviewAsync()
    {
        try
        {
            var preview = await _launchService.BuildCommandPreviewAsync(_game).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => CommandPreview = preview ?? "Emülatör yolu ayarlanmamış.");
        }
        catch (Exception ex)
        {
            Logger.Warning(nameof(GameDetailsViewModel), "Komut önizlemesi üretilemedi: " + ex.Message);
        }
    }

    partial void OnPlatformIdChanged(PlatformId value)
        => _ = RefreshPreviewAsync();

    partial void OnFullscreenModeChanged(FullscreenMode value)
        => _ = RefreshPreviewAsync();

    partial void OnExtraArgumentsChanged(string? value)
        => _ = RefreshPreviewAsync();

    partial void OnArgumentTemplateChanged(string? value)
        => _ = RefreshPreviewAsync();
}
