using System.Diagnostics;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Application.Services;

namespace GameShelf.Application.ViewModels;

public sealed partial class DownloadRow : ObservableObject
{
    public DownloadCatalogItem Item { get; init; } = null!;

    [ObservableProperty] private string _statusText = "Bilinmiyor";

    [ObservableProperty] private double _progress;

    [ObservableProperty] private bool _isWorking;

    [ObservableProperty] private string _installedPath = string.Empty;
}

/// <summary>
/// İndirme Merkezi: emülatörler ve yardımcı araçlar RESMÎ adreslerden indirilir.
/// Oyun, BIOS/firmware veya anahtar bu ekranda ASLA yer almaz.
/// </summary>
public sealed partial class DownloadsViewModel : ViewModelBase
{
    private readonly DownloadService _downloads;
    private readonly EmulatorSetupService _setup;
    private readonly IShellService _shell;

    public DownloadsViewModel(
        DownloadService downloads,
        EmulatorSetupService setup,
        IShellService shell,
        ILoggingService logger,
        IDispatcher dispatcher)
        : base(logger, dispatcher)
    {
        _downloads = downloads;
        _setup = setup;
        _shell = shell;
    }

    public ObservableCollection<DownloadRow> Rows { get; } = new();

    public string Note =>
        "GameShelf hiçbir emülatörü, BIOS/firmware dosyasını veya oyunu kendi içinde taşımaz. " +
        "Bu ekrandaki her indirme, ilgili projenin RESMÎ adresinden yapılır ve yalnızca program dosyasını içerir. " +
        "Oyun dosyalarını ve BIOS'ları kendi yasal yedeklerinizden sağlamanız gerekir.";

    // ------------------------------------------------------------------ komutlar

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (Rows.Count == 0)
        {
            foreach (var item in _downloads.Catalog)
            {
                Rows.Add(new DownloadRow { Item = item, StatusText = "Durum okunuyor..." });
            }
        }

        foreach (var row in Rows)
        {
            row.Progress = 0;

            if (row.Item.PlatformKey is { } key)
            {
                var found = await Task.Run(() => _setup.DetectExecutable(key)).ConfigureAwait(false);
                row.InstalledPath = found ?? string.Empty;
                row.StatusText = found is null ? "Kurulu değil — indirip kurun" : $"Hazır: {found}";
            }
            else if (row.Item.Id == "7zip")
            {
                row.StatusText = DownloadService.Find7Zip() is null
                    ? "Kurulu değil — .7z dosyaları için gerekli"
                    : "Hazır";
            }
            else
            {
                row.StatusText = "Gerekirse indirin";
            }
        }
    }

    [RelayCommand]
    private async Task DownloadAsync(DownloadRow? row)
    {
        if (row is null || row.IsWorking)
        {
            return;
        }

        row.IsWorking = true;
        row.Progress = 0;
        row.StatusText = "İndiriliyor...";

        var key = row.Item.PlatformKey;

        var target = key is { } platformKey
            ? EmulatorSetupService.EmulatorDirectoryFor(platformKey)
            : Path.Combine(DownloadService.DownloadRoot, row.Item.Id);

        var progress = new Progress<double>(value => _ = Dispatcher.InvokeAsync(() =>
        {
            row.Progress = value;
            row.StatusText = $"İndiriliyor: %{value * 100:0}";
        }));

        try
        {
            var outcome = await _downloads.DownloadAsync(row.Item, target, progress, CancellationToken.None)
                .ConfigureAwait(false);

            if (!outcome.Success)
            {
                row.StatusText = outcome.Message;
                return;
            }

            if (row.Item.Kind == DownloadKind.Installer)
            {
                row.InstalledPath = outcome.Path ?? string.Empty;

                if (outcome.Path is { } installer && File.Exists(installer))
                {
                    DownloadService.RunInstaller(installer);
                    row.StatusText = "Kurulum programı açıldı — kurulumu tamamlayın, sonra tekrar deneyin.";
                }
                else
                {
                    row.StatusText = "İndirildi: " + outcome.Path;
                }

                return;
            }

            if (key is { } platform)
            {
                var found = await Task.Run(() => _setup.DetectExecutable(platform)).ConfigureAwait(false);

                if (found is null)
                {
                    row.StatusText = $"İndirildi, ama exe bulunamadı. Klasör: {target}";
                    return;
                }

                await _setup.SaveExecutablePathAsync(platform, found).ConfigureAwait(false);
                row.InstalledPath = found;
                row.Progress = 1;
                row.StatusText = $"Hazır: {found}";
                return;
            }

            row.StatusText = "Tamamlandı: " + outcome.Path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            row.StatusText = "Hata: " + ex.Message;
        }
        finally
        {
            row.IsWorking = false;
        }
    }

    /// <summary>Kalemin resmî sayfasını tarayıcıda açar (emülatör veya araç).</summary>
    [RelayCommand]
    private void OpenPage(DownloadRow? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(row.Item.PageUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(row.Item.PageUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenFolder(DownloadRow? row)
    {
        if (row is null)
        {
            return;
        }

        var folder = row.Item.PlatformKey is { } key
            ? EmulatorSetupService.EmulatorDirectoryFor(key)
            : DownloadService.DownloadRoot;

        Directory.CreateDirectory(folder);
        _shell.OpenFolder(folder);
    }
}
