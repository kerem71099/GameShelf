using System.Diagnostics;
using GameShelf.Application.Models;

namespace GameShelf.Application.Services;

/// <summary>
/// Emülatör kurulum yardımcısı.
/// YASAL SINIR: bu uygulama emülatör ikili dosyası, BIOS/firmware, anahtar veya oyun dosyası
/// DAĞITMAZ. Yalnızca (1) resmî indirme sayfasını açar, (2) diskte kurulu emülatörü arar,
/// (3) kullanıcının kendi oyun klasörünü hazırlar.
/// </summary>
public sealed class EmulatorSetupService
{
    public const string Notice =
        "Emülatörler GameShelf ile birlikte gelmez (ayrı GPL projeleridir ve sık güncellenir); " +
        "'İndir' düğmesi yalnızca resmî indirme sayfasını açar. " +
        "BIOS/firmware ve oyun dosyaları hiçbir zaman dahil değildir: kendi yasal yedeklerinizi kullanın. " +
        "İpucu: emülatörleri EXE'nin yanındaki 'emulators' klasörüne koyarsanız 'Otomatik ara' bulur.";

    /// <summary>Kullanıcının kendi oyun klasörü (%USERPROFILE%\GameShelf\Games).</summary>
    public static string GamesDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "GameShelf", "Games");

    /// <summary>Tek EXE'nin yanındaki emülatör klasörü (taşınabilir kurulum).</summary>
    public static string BundledEmulatorDirectory => Path.Combine(AppContext.BaseDirectory, "emulators");

    public IReadOnlyList<EmulatorDownloadInfo> Downloads { get; } = new[]
    {
        new EmulatorDownloadInfo(
            "Ps1", "PS1", "DuckStation",
            "https://github.com/stenzek/duckstation/releases/latest",
            "cue/bin, chd, iso, pbp destekler. Konsolunuzdan aldığınız BIOS'u DuckStation'a kendiniz gösterirsiniz."),
        new EmulatorDownloadInfo(
            "Ps2", "PS2", "PCSX2",
            "https://github.com/PCSX2/pcsx2/releases/latest",
            "iso ve chd destekler. PS2 BIOS'unu PCSX2 içinde kendiniz tanıtırsınız."),
        new EmulatorDownloadInfo(
            "Ps3", "PS3", "RPCS3",
            "https://github.com/RPCS3/rpcs3/releases/latest",
            "Klasör yapısı (PS3_GAME/USRDIR/EBOOT.BIN) ve iso destekler. PS3 firmware'ini RPCS3 ile kendiniz kurarsınız."),
        new EmulatorDownloadInfo(
            "Ps4", "PS4", "—",
            "https://www.playstation.com/",
            "PS4 için yasal bir emülatör yok. Kart yalnızca bilgi amaçlıdır."),
        new EmulatorDownloadInfo(
            "Ps5", "PS5", "—",
            "https://www.playstation.com/",
            "PS5 için yasal bir emülatör yok. Kart yalnızca bilgi amaçlıdır."),
    };

    /// <summary>Bilinen konumlarda kurulu emülatörü arar; bulamazsa null.</summary>
    public string? DetectExecutable(string platformKey)
    {
        var names = platformKey switch
        {
            "Ps1" => new[]
            {
                "duckstation-qt-x64-ReleaseLTCG.exe", "duckstation-qt-x64.exe",
                "duckstation-nogui-x64-ReleaseLTCG.exe", "duckstation.exe"
            },
            "Ps2" => new[] { "pcsx2-qtx64-avx2.exe", "pcsx2-qtx64.exe", "pcsx2-qt.exe", "pcsx2.exe" },
            "Ps3" => new[] { "rpcs3.exe" },
            _ => Array.Empty<string>()
        };

        if (names.Length == 0)
        {
            return null;
        }

        foreach (var directory in CandidateDirectories(platformKey))
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                foreach (var name in names)
                {
                    var direct = Path.Combine(directory, name);
                    if (File.Exists(direct))
                    {
                        return direct;
                    }
                }

                // bir alt klasöre de bak (örn. emulators\DuckStation\duckstation.exe)
                foreach (var sub in Directory.EnumerateDirectories(directory).Take(60))
                {
                    foreach (var name in names)
                    {
                        var candidate = Path.Combine(sub, name);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // erişilemeyen klasörleri sessizce atla
            }
        }

        return null;
    }

    /// <summary>Resmî indirme sayfasını varsayılan tarayıcıda açar.</summary>
    public void OpenOfficialPage(string platformKey)
    {
        var info = Downloads.FirstOrDefault(d =>
            string.Equals(d.PlatformKey, platformKey, StringComparison.OrdinalIgnoreCase));

        if (info is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(info.DownloadUrl) { UseShellExecute = true });
    }

    /// <summary>Kullanıcının kendi oyun klasörünü oluşturur (varsa dokunmaz).</summary>
    public static void EnsureGamesDirectory()
    {
        try
        {
            Directory.CreateDirectory(GamesDirectory);
        }
        catch (Exception)
        {
            // klasör oluşturulamazsa kullanıcı elle ekleyebilir; uygulamayı düşürmeyelim
        }
    }

    private static IEnumerable<string> CandidateDirectories(string platformKey)
    {
        yield return BundledEmulatorDirectory;
        yield return Path.Combine(BundledEmulatorDirectory, platformKey);
        yield return AppContext.BaseDirectory;

        // Kaynaktan (dotnet run) çalıştırırken emulators\ depo kökünde kalır;
        // EXE'nin birkaç üst klasörüne de bakalım.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        for (var i = 0; i < 6 && directory is not null; i++)
        {
            yield return Path.Combine(directory.FullName, "emulators");
            yield return Path.Combine(directory.FullName, "emulators", platformKey);
            directory = directory.Parent;
        }

        var vendor = platformKey switch
        {
            "Ps1" => "DuckStation",
            "Ps2" => "PCSX2",
            "Ps3" => "RPCS3",
            _ => string.Empty
        };

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            yield return Path.Combine(root, "Programs");

            if (vendor.Length == 0)
            {
                continue;
            }

            yield return Path.Combine(root, vendor);
            yield return Path.Combine(root, "Programs", vendor);
        }
    }
}
