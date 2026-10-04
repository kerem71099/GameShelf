using System.Text;
using System.Text.RegularExpressions;
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
    private const long Ps2SizeThreshold = 700L * 1024 * 1024;   // PS1 CD'leri ~700 MB altindadir
    // PS2 DVD-9 imajlari ~8.1 GB' a kadar cikabilir; PS3 Blu-ray oyunlari bundan buyuktur.
    private const long Ps3SizeThreshold = 9L * 1024 * 1024 * 1024;

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
            // 5a) Dosya adındaki seri numarası (örn. "SLUS_209.46.Game.iso")
            var nameSerial = ExtractSerial(Path.GetFileNameWithoutExtension(path));

            if (nameSerial is { } named && ClassifySerial(named) is { } namedPlatform)
            {
                return new PlatformDetection(namedPlatform, 0.75, $"Dosya adındaki seri: {named}");
            }

            var detection = await InspectDiscImageAsync(path, hint, cancellationToken).ConfigureAwait(false);
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

    private static async Task<PlatformDetection> InspectDiscImageAsync(
        string path,
        PlatformId? hint,
        CancellationToken cancellationToken)
    {
        var sawSystemCnf = false;
        var sawBoot2 = false;
        var sawBoot = false;
        string? serial = null;
        long length = 0;

        try
        {
            length = new FileInfo(path).Length;

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
                    return new PlatformDetection(PlatformId.Ps3, 0.95, "PS3_DISC.SFB bulundu");
                }

                // ÖNEMLİ: işaretler farklı bloklara düşebilir; bayrak olarak topla,
                // kararı tarama bitince ver (eski sürüm ikisini aynı blokta arıyordu).
                if (Contains(haystack, "SYSTEM.CNF"))
                {
                    sawSystemCnf = true;
                }

                if (Contains(haystack, "BOOT2"))
                {
                    sawBoot2 = true;
                }

                if (Contains(haystack, "BOOT =") || Contains(haystack, "BOOT="))
                {
                    sawBoot = true;
                }

                serial ??= ExtractSerial(haystack);

                carry = haystack.Length > 256 ? haystack[^256..] : haystack;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Okunamadı -> belirsiz
        }

        // 1) SYSTEM.CNF içeriği: BOOT2 -> PS2, BOOT -> PS1
        if (sawSystemCnf && sawBoot2)
        {
            return new PlatformDetection(PlatformId.Ps2, 0.92, "SYSTEM.CNF + BOOT2 (PS2)");
        }

        if (sawSystemCnf && sawBoot)
        {
            return new PlatformDetection(PlatformId.Ps1, 0.8, "SYSTEM.CNF + BOOT (PS1)");
        }

        if (sawSystemCnf)
        {
            // Yalnızca SYSTEM.CNF var: DVD boyutu PS2, CD boyutu PS1 demektir.
            return length >= Ps2SizeThreshold
                ? new PlatformDetection(PlatformId.Ps2, 0.62, "SYSTEM.CNF + DVD boyutu (PS2 tahmini)")
                : new PlatformDetection(PlatformId.Ps1, 0.55, "SYSTEM.CNF + CD boyutu (PS1 tahmini)");
        }

        // 2) Disk seri numarası: SLUS_209.46 / SLES-53427 / SCPS-10100 ...
        if (serial is { } found)
        {
            var guess = ClassifySerial(found);

            if (guess is { } platform)
            {
                return new PlatformDetection(platform, 0.7, $"Disk seri numarası: {found}");
            }
        }

        // 3) Son çare: boyut. Klasör ipucu varsa ipucu kazanır.
        if (length >= Ps3SizeThreshold)
        {
            return hint is { } ps3Hint
                ? new PlatformDetection(ps3Hint, 0.45, $"klasör ipucu + Blu-ray boyutu ({ps3Hint.ToShortName()})")
                : new PlatformDetection(PlatformId.Ps3, 0.4, "Boyut: Blu-ray (PS3 tahmini)");
        }

        if (length >= Ps2SizeThreshold)
        {
            return hint is { } ps2Hint
                ? new PlatformDetection(ps2Hint, 0.45, $"klasör ipucu + DVD boyutu ({ps2Hint.ToShortName()})")
                : new PlatformDetection(PlatformId.Ps2, 0.4, "Boyut: DVD (PS2 tahmini)");
        }

        return new PlatformDetection(PlatformId.Unknown, 0.15, "ISO içinde platform işareti bulunamadı");
    }

    /// <summary>ISO metninden "SLUS_209.46" / "SLES-53427" gibi bir disk serisi yakalar.</summary>
    private static string? ExtractSerial(string text)
    {
        var match = Regex.Match(text, @"\b(S[LC][AEJP][SM])\s*[-_]\s*(\d{2,5})(\.\d{1,2})?");

        return match.Success ? $"{match.Groups[1].Value}-{match.Groups[2].Value}" : null;
    }

    /// <summary>
    /// Disk serisinden platform tahmini:
    /// 0xxxx / 1xxxx -> PS1, 2xxxx / 5xxxx / 6xxxx / 7xxxx -> PS2, 94xxx -> PS1, 97xxx -> PS2.
    /// </summary>
    private static PlatformId? ClassifySerial(string serial)
    {
        var digits = serial.Split('-')[1];

        return digits[0] switch
        {
            '0' => PlatformId.Ps1,
            '1' => PlatformId.Ps1,
            '2' => PlatformId.Ps2,
            '5' => PlatformId.Ps2,
            '6' => PlatformId.Ps2,
            '7' => PlatformId.Ps2,
            '9' => digits.StartsWith("97", StringComparison.Ordinal) ? PlatformId.Ps2
                 : digits.StartsWith("94", StringComparison.Ordinal) ? PlatformId.Ps1
                 : null,
            _ => null
        };
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
