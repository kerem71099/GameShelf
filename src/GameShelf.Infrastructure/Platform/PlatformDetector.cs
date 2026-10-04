using System.Text;
using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Extensions;

namespace GameShelf.Infrastructure.Platform;

/// <summary>
/// Platform tahmini: dizin yapısı → magic byte → uzantı → klasör ipucu → Unknown.
/// Emin olmadığı durumda ASLA zorla platform atamaz; kullanıcıya sorulur.
/// </summary>
public sealed class PlatformDetector : IPlatformDetector
{
    private const int IsoScanLimit = 4 * 1024 * 1024; // ISO içi marker taraması için ilk 4 MB
    private const int BufferSize = 1024 * 1024;

    public IReadOnlyList<string> KnownExtensions => PlatformCatalog.AllExtensions;

    public async Task<PlatformDetection> DetectAsync(
        string path,
        PlatformId? hint = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PlatformDetection.Unknown;
        }

        // 1) PS3 benzeri klasör yapıları
        if (Directory.Exists(path))
        {
            return await IsGameDirectoryAsync(path, cancellationToken).ConfigureAwait(false)
                ? new PlatformDetection(PlatformId.Ps3, 0.95, "PS3 klasör yapısı (PS3_GAME / PS3_DISC.SFB)")
                : PlatformDetection.Unknown;
        }

        if (!File.Exists(path))
        {
            return PlatformDetection.Unknown;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();

        // 2) Cue sheet → PS1
        if (extension == ".cue")
        {
            var text = await ReadHeadAsync(path, 4096, cancellationToken).ConfigureAwait(false);
            return text.Contains("FILE", StringComparison.OrdinalIgnoreCase)
                ? new PlatformDetection(PlatformId.Ps1, 0.9, "CUE sheet (bin/cue)")
                : new PlatformDetection(PlatformId.Ps1, 0.6, ".cue uzantısı");
        }

        // 3) CHD kabı → platform bilgisi yok; klasör ipucu kullanılır
        if (extension == ".chd")
        {
            return hint is { } chdHint
                ? new PlatformDetection(chdHint, 0.5, $"CHD kabı + klasör ipucu ({chdHint.ToShortName()})")
                : new PlatformDetection(PlatformId.Unknown, 0.2, "CHD kabı: platform belirsiz, kullanıcı seçmeli");
        }

        // 4) PBP (PS1 eboot) / CSO (PS2)
        if (extension == ".pbp")
        {
            return new PlatformDetection(PlatformId.Ps1, 0.8, ".pbp (PS1)");
        }

        if (extension == ".cso")
        {
            return new PlatformDetection(PlatformId.Ps2, 0.6, ".cso (PS2)");
        }

        // 5) ISO/IMG: içindeki ASCII marker'lara bak
        if (extension is ".iso" or ".bin" or ".img" or ".mdf")
        {
            var detection = await InspectDiscImageAsync(path, cancellationToken).ConfigureAwait(false);
            if (detection.PlatformId != PlatformId.Unknown)
            {
                return detection;
            }
        }

        // 6) Sadece uzantı + klasör ipucu
        return hint is { } fallback
            ? new PlatformDetection(fallback, 0.4, $"uzantı + klasör ipucu ({fallback.ToShortName()})")
            : new PlatformDetection(PlatformId.Unknown, 0.1, "Tespit edilemedi; kullanıcı seçmeli");
    }

    public Task<bool> IsGameDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return Task.FromResult(false);
        }

        try
        {
            if (Directory.Exists(Path.Combine(path, "PS3_GAME")))
            {
                return Task.FromResult(true);
            }

            if (File.Exists(Path.Combine(path, "PS3_DISC.SFB")))
            {
                return Task.FromResult(true);
            }

            if (File.Exists(Path.Combine(path, "PARAM.SFO")) && Directory.Exists(Path.Combine(path, "USRDIR")))
            {
                return Task.FromResult(true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(false);
    }

    // ------------------------------------------------------------------ yardımcılar

    private static async Task<PlatformDetection> InspectDiscImageAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, useAsync: true);

            var buffer = new byte[BufferSize];
            var carry = string.Empty;
            long total = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0
                   && total < IsoScanLimit)
            {
                total += read;

                // NULL'ları boşluğa çevir ki metin taraması kolay olsun
                var chunk = Encoding.ASCII.GetString(buffer, 0, read).Replace('\0', ' ');
                var haystack = carry + chunk;

                if (Contains(haystack, "PS3_DISC.SFB"))
                {
                    return new PlatformDetection(PlatformId.Ps3, 0.9, "PS3_DISC.SFB bulundu");
                }

                if (Contains(haystack, "SYSTEM.CNF"))
                {
                    if (Contains(haystack, "BOOT2"))
                    {
                        return new PlatformDetection(PlatformId.Ps2, 0.85, "SYSTEM.CNF + BOOT2 (PS2)");
                    }

                    if (Contains(haystack, "BOOT"))
                    {
                        return new PlatformDetection(PlatformId.Ps1, 0.7, "SYSTEM.CNF + BOOT (PS1)");
                    }
                }

                carry = haystack.Length > 128 ? haystack[^128..] : haystack;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Okunamadı → belirsiz
        }

        return new PlatformDetection(PlatformId.Unknown, 0.15, "ISO içinde platform işareti bulunamadı");
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static async Task<string> ReadHeadAsync(string path, int byteCount, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, byteCount, useAsync: true);
            var buffer = new byte[byteCount];
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            return Encoding.ASCII.GetString(buffer, 0, read).Replace('\0', ' ');
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
