using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Services;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Extensions;

namespace GameShelf.Application.ViewModels;

/// <summary>Kütüphane listesindeki tek satır/kart. UI'da anında güncelleme için sarmalayıcı.</summary>
public sealed partial class GameItemViewModel : ViewModelBase
{
    private readonly Game _game;

    public GameItemViewModel(Game game, ILoggingService logger, IDispatcher dispatcher)
        : base(logger, dispatcher)
    {
        _game = game;
    }

    public Game Model => _game;

    public Guid Id => _game.Id;

    public PlatformId PlatformId => _game.PlatformId;

    public string PlatformShort => _game.PlatformId.ToShortName();

    public bool IsLaunchable => _game.PlatformId.IsLaunchable();

    public bool IsMissing => _game.IsMissing;

    public string FilePath => _game.FilePath;

    public string Title
    {
        get => _game.Title;
        set
        {
            if (_game.Title == value)
            {
                return;
            }

            _game.Title = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Initials));
        }
    }

    public string? Region
    {
        get => _game.Region;
        set
        {
            if (_game.Region == value)
            {
                return;
            }

            _game.Region = value;
            OnPropertyChanged();
        }
    }

    public string? Notes
    {
        get => _game.Notes;
        set
        {
            if (_game.Notes == value)
            {
                return;
            }

            _game.Notes = value;
            OnPropertyChanged();
        }
    }

    public string? CoverPath
    {
        get => _game.CoverPath;
        set
        {
            if (_game.CoverPath == value)
            {
                return;
            }

            _game.CoverPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasCover));
        }
    }

    public bool HasCover => !string.IsNullOrWhiteSpace(_game.CoverPath) && File.Exists(_game.CoverPath);

    public bool IsFavorite
    {
        get => _game.IsFavorite;
        set
        {
            if (_game.IsFavorite == value)
            {
                return;
            }

            _game.IsFavorite = value;
            OnPropertyChanged();
        }
    }

    public string Initials => TitleCleaner.Initials(_game.Title);

    public string PlayTimeText => _game.PlayTimeMinutes switch
    {
        0 => "—",
        < 60 => $"{_game.PlayTimeMinutes} dk",
        _ => $"{_game.PlayTimeMinutes / 60}s {_game.PlayTimeMinutes % 60}dk"
    };

    public string LastPlayedText => _game.LastPlayedAt is null
        ? "Hiç oynanmadı"
        : _game.LastPlayedAt.Value.LocalDateTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);

    public string SizeText => _game.FileSizeBytes <= 0
        ? string.Empty
        : $"{_game.FileSizeBytes / (1024d * 1024 * 1024):0.##} GB";

    public string Subtitle => $"{PlatformShort} · {PlayTimeText}";

    /// <summary>Model üzerinde yapılan değişikliklerden sonra tüm bağlamaları tazeler.</summary>
    public void RefreshAll()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Region));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(CoverPath));
        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(PlatformId));
        OnPropertyChanged(nameof(PlatformShort));
        OnPropertyChanged(nameof(IsLaunchable));
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(PlayTimeText));
        OnPropertyChanged(nameof(LastPlayedText));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(Subtitle));
    }
}
