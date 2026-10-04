using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameShelf.Application.Models;

namespace GameShelf.Application.Services;

public sealed record DownloadOutcome(bool Success, string Message, string? Path);

/// <summary>
/// İndirme Merkezi'nin arka planı: GitHub'daki RESMÎ sürümlerden dosya indirir,
/// zip'i kendisi açar, 7z için 7-Zip kullanır (yoksa haber verir).
/// Yalnızca emülatör ve yardımcı ARAÇ indirir; oyun/BIOS/anahtar asla indirmez.
/// </summary>
public sealed class DownloadService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(30) };

    public DownloadService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GameShelf", "0.1"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Geçici indirme klasörü.</summary>
    public static string DownloadRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameShelf", "downloads");

    public IReadOnlyList<DownloadCatalogItem> Catalog { get; } = new[]
    {
        new DownloadCatalogItem(
            "duckstation", "Emülatör", "DuckStation — PS1",
            "Resmî sürüm (stenzek/duckstation). cue/bin, chd, iso, pbp açar.",
            DownloadKind.Extract, "Ps1", "stenzek", "duckstation",
            @"windows-x64-release\.zip$", @"windows.*x64.*\.(zip|7z)$", null,
            "https://github.com/stenzek/duckstation/releases/latest"),

        new DownloadCatalogItem(
            "pcsx2", "Emülatör", "PCSX2 — PS2",
            "Resmî sürüm (PCSX2/pcsx2). iso ve chd açar. .7z için 7-Zip gerekir.",
            DownloadKind.Extract, "Ps2", "PCSX2", "pcsx2",
            @"windows.*(x64|64bit).*\.(zip|7z)$", @"windows.*\.(zip|7z)$", null,
            "https://github.com/PCSX2/pcsx2/releases/latest"),

        new DownloadCatalogItem(
            "rpcs3", "Emülatör", "RPCS3 — PS3",
            "Resmî sürüm (RPCS3/rpcs3). Klasör yapısı ve iso açar. .7z için 7-Zip gerekir.",
            DownloadKind.Extract, "Ps3", "RPCS3", "rpcs3",
            @"win64.*\.(zip|7z)$", @"windows.*\.(zip|7z)$", null,
            "https://github.com/RPCS3/rpcs3/releases/latest"),

        new DownloadCatalogItem(
            "7zip", "Araç", "7-Zip (arşiv açıcı)",
            "PCSX2 ve RPCS3 .7z olarak gelir; bu araç onları açmak için gerekir.",
            DownloadKind.Installer, null, "ip7z", "7zip",
            @"^7z\d{3,4}-x64\.exe$", null, null,
            "https://www.7-zip.org/download.html"),

        new DownloadCatalogItem(
            "dotnet8", "Araç", ".NET 8 Desktop Runtime",
            "Yalnızca 'framework-dependent' EXE'yi başka PC'de çalıştırmak için gerekir "
            + "(önerilen self-contained EXE için gerekmez).",
            DownloadKind.Installer, null, null, null, null, null,
            "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe",
            "https://dotnet.microsoft.com/download/dotnet/8.0")
    };

    public async Task<DownloadOutcome> DownloadAsync(
        DownloadCatalogItem item,
        string targetDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(DownloadRoot);
            Directory.CreateDirectory(targetDirectory);

            var url = item.DirectUrl;

            if (string.IsNullOrWhiteSpace(url))
            {
                url = await ResolveGitHubAssetAsync(item, cancellationToken).ConfigureAwait(false);
            }

            var fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = item.Id + ".bin";
            }

            var tempFile = Path.Combine(DownloadRoot, fileName);

            await DownloadFileAsync(url, tempFile, progress, cancellationToken).ConfigureAwait(false);

            if (item.Kind == DownloadKind.Installer)
            {
                progress?.Report(1);
                return new DownloadOutcome(true, $"İndirildi: {tempFile}", tempFile);
            }

            return await ExtractAsync(tempFile, targetDirectory, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new DownloadOutcome(false, ex.Message, null);
        }
    }

    /// <summary>Kurulum programını kullanıcı onayıyla çalıştırır (7-Zip / .NET runtime gibi).</summary>
    public static void RunInstaller(string path)
        => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    private async Task<DownloadOutcome> ExtractAsync(
        string archive,
        string targetDirectory,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Run(() => ZipFile.ExtractToDirectory(archive, targetDirectory, overwriteFiles: true),
                cancellationToken).ConfigureAwait(false);

            progress?.Report(1);
            return new DownloadOutcome(true, $"Açıldı: {targetDirectory}", targetDirectory);
        }

        if (archive.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
        {
            var sevenZip = Find7Zip();

            if (sevenZip is null)
            {
                return new DownloadOutcome(false,
                    "7-Zip bulunamadı. Önce listeden 7-Zip'i indirip kurun, sonra tekrar deneyin.",
                    archive);
            }

            var info = new ProcessStartInfo(sevenZip)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            info.ArgumentList.Add("x");
            info.ArgumentList.Add(archive);
            info.ArgumentList.Add($"-o{targetDirectory}");
            info.ArgumentList.Add("-y");

            using var process = Process.Start(info);

            if (process is null)
            {
                return new DownloadOutcome(false, "7-Zip başlatılamadı.", null);
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            progress?.Report(1);

            return process.ExitCode == 0
                ? new DownloadOutcome(true, $"Açıldı: {targetDirectory}", targetDirectory)
                : new DownloadOutcome(false, $"7-Zip hata kodu: {process.ExitCode}", null);
        }

        return new DownloadOutcome(false, $"Desteklenmeyen arşiv: {Path.GetFileName(archive)}", archive);
    }

    public static string? Find7Zip()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task<string> ResolveGitHubAssetAsync(DownloadCatalogItem item, CancellationToken cancellationToken)
    {
        var json = await _http
            .GetStringAsync($"https://api.github.com/repos/{item.Owner}/{item.Repo}/releases/latest", cancellationToken)
            .ConfigureAwait(false);

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("assets", out var assets))
        {
            throw new InvalidOperationException("Sürümde indirilebilir dosya bulunamadı.");
        }

        string? fallback = null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            var url = asset.GetProperty("browser_download_url").GetString();

            if (url is null)
            {
                continue;
            }

            if (Regex.IsMatch(name, item.AssetPattern ?? ".", RegexOptions.IgnoreCase))
            {
                return url;
            }

            if (fallback is null && item.AssetFallback is { } pattern && Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase))
            {
                fallback = url;
            }
        }

        return fallback ?? throw new InvalidOperationException("Bu sürümde uygun Windows dosyası yok.");
    }

    private async Task DownloadFileAsync(string url, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var response = await _http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        var buffer = new byte[81920];
        long transferred = 0;

        await using (var network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var file = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true))
        {
            int read;

            while ((read = await network.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                transferred += read;

                if (total > 0)
                {
                    progress?.Report((double)transferred / total);
                }
            }
        }
    }

    public void Dispose() => _http.Dispose();
}
