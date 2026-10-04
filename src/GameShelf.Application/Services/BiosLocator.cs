using GameShelf.Domain.Enums;

namespace GameShelf.Application.Services;

/// <summary>
/// Kullanıcının diskinde ZATEN VAR olan BIOS/firmware klasörünü bulur.
/// <para>
/// YASAL SINIR: bu sınıf hiçbir dosya indirmez, kopyalamaz, üretmez veya yazmaz.
/// Yalnızca emülatörün kendi kurulum dizininde (kullanıcının daha önce oluşturduğu)
/// dosyaları OKUR ve klasör yolunu döndürür. BIOS'u kullanıcı kendi konsolundan
/// edinmek zorundadır; GameShelf yalnızca yolu işaretler.
/// </para>
/// </summary>
public static class BiosLocator
{
    /// <summary>4 MB: standart PS2 BIOS görüntüsü (SCPH-xxxxx.bin).</summary>
    private const long Ps2BiosSize = 4L * 1024 * 1024;

    /// <summary>512 KB: standart PS1 BIOS görüntüsü (scph1001.bin vb.).</summary>
    private const long Ps1BiosSize = 512L * 1024;

    /// <summary>
    /// Platform için BIOS/firmware klasörünü arar; bulamazsa null.
    /// <paramref name="emulatorExePath"/> verilirse emülatörün kendi dizini önce aranır
    /// (taşınabilir kurulumlarda 'bios' klasörü exe'nin yanındadır).
    /// </summary>
    public static string? Find(PlatformId platformId, string? emulatorExePath = null)
    {
        foreach (var directory in CandidateDirectories(platformId, emulatorExePath))
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                if (ContainsBios(platformId, directory))
                {
                    return directory;
                }
            }
            catch (Exception)
            {
                // erişilemeyen/bozuk klasörleri sessizce atla
            }
        }

        return null;
    }

    /// <summary>Aranacak klasörler: emülatörün kendi dizini önce, sonra bilinen konumlar.</summary>
    public static IReadOnlyList<string> CandidateDirectories(PlatformId platformId, string? emulatorExePath)
    {
        var directories = new List<string>();

        if (!string.IsNullOrWhiteSpace(emulatorExePath))
        {
            var exeDirectory = Path.GetDirectoryName(emulatorExePath);

            if (!string.IsNullOrWhiteSpace(exeDirectory))
            {
                AddEmulatorRelative(directories, platformId, exeDirectory);

                // emulators\PS2\pcsx2-qtx64.exe → emulators\PS2 (bir üst klasördeki bios/)
                var parent = Directory.GetParent(exeDirectory);

                if (parent is not null)
                {
                    AddEmulatorRelative(directories, platformId, parent.FullName);
                }
            }
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programsX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        switch (platformId)
        {
            case PlatformId.Ps2:
                AddKnown(directories, documents, "PCSX2", "bios");
                AddKnown(directories, roaming, "PCSX2", "bios");
                AddKnown(directories, local, "PCSX2", "bios");
                AddKnown(directories, profile, "PCSX2", "bios");
                AddKnown(directories, programs, "PCSX2", "bios");
                AddKnown(directories, programsX86, "PCSX2", "bios");
                AddKnown(directories, local, "Programs", "PCSX2", "bios");
                break;

            case PlatformId.Ps1:
                AddKnown(directories, documents, "DuckStation", "bios");
                AddKnown(directories, roaming, "DuckStation", "bios");
                AddKnown(directories, local, "DuckStation", "bios");
                AddKnown(directories, profile, "DuckStation", "bios");
                AddKnown(directories, programs, "DuckStation", "bios");
                AddKnown(directories, programsX86, "DuckStation", "bios");
                AddKnown(directories, local, "Programs", "DuckStation", "bios");
                break;

            case PlatformId.Ps3:
                AddKnown(directories, profile, "RPCS3", "dev_flash");
                AddKnown(directories, local, "RPCS3", "dev_flash");
                AddKnown(directories, roaming, "RPCS3", "dev_flash");
                AddKnown(directories, documents, "RPCS3", "dev_flash");
                AddKnown(directories, programs, "RPCS3", "dev_flash");
                AddKnown(directories, programsX86, "RPCS3", "dev_flash");
                break;
        }

        return directories;
    }

    /// <summary>
    /// Kullanıcının BIOS dosyasını koyması gereken klasör (emülatörün kendi klasörü).
    /// Yalnızca YOL bilgisidir: klasörü oluşturmaz, içine hiçbir şey yazmaz.
    /// </summary>
    public static string? RecommendedDirectory(PlatformId platformId, string? emulatorExePath)
        => CandidateDirectories(platformId, emulatorExePath)
            .FirstOrDefault(directory => !string.IsNullOrWhiteSpace(directory));

    private static void AddEmulatorRelative(List<string> directories, PlatformId platformId, string baseDirectory)
    {
        if (platformId == PlatformId.Ps3)
        {
            // RPCS3 firmware'i dev_flash klasöründe durur.
            directories.Add(Path.Combine(baseDirectory, "dev_flash"));
            return;
        }

        directories.Add(Path.Combine(baseDirectory, "bios"));
    }

    private static void AddKnown(List<string> directories, string? root, params string[] parts)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        var path = root;

        foreach (var part in parts)
        {
            path = Path.Combine(path, part);
        }

        directories.Add(path);
    }

    /// <summary>Klasör gerçekten BIOS/firmware içeriyor mu? (yalnızca okuma)</summary>
    private static bool ContainsBios(PlatformId platformId, string directory)
    {
        switch (platformId)
        {
            case PlatformId.Ps2:
                return Files(directory).Any(IsPlayStation2Bios);

            case PlatformId.Ps1:
                return Files(directory).Any(IsPlayStationBios);

            case PlatformId.Ps3:
                // dev_flash: RPCS3'in kurduğu PS3 firmware ağacı.
                return Directory.Exists(Path.Combine(directory, "sys", "external"))
                       || Directory.Exists(Path.Combine(directory, "vsh", "module"))
                       || Directory.Exists(Path.Combine(directory, "data"))
                       || Directory.EnumerateFileSystemEntries(directory).Take(1).Any();

            default:
                return false;
        }
    }

    private static IEnumerable<FileInfo> Files(string directory)
        => new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.TopDirectoryOnly);

    private static bool IsPlayStation2Bios(FileInfo file)
    {
        if (!string.Equals(file.Extension, ".bin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (file.Length == Ps2BiosSize)
        {
            return true;
        }

        var name = file.Name.ToLowerInvariant();

        var looksLikePs2 = name.StartsWith("scph", StringComparison.Ordinal)
                           || name.StartsWith("ps2-", StringComparison.Ordinal)
                           || name.StartsWith("ps2bios", StringComparison.Ordinal)
                           || name.StartsWith("erom", StringComparison.Ordinal)
                           || name.StartsWith("rom1", StringComparison.Ordinal)
                           || name.StartsWith("rom2", StringComparison.Ordinal);

        return looksLikePs2 && file.Length >= Ps1BiosSize && file.Length <= 16L * 1024 * 1024;
    }

    private static bool IsPlayStationBios(FileInfo file)
    {
        if (!string.Equals(file.Extension, ".bin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (file.Length == Ps1BiosSize)
        {
            return true;
        }

        var name = file.Name.ToLowerInvariant();

        var looksLikePs1 = name.StartsWith("scph", StringComparison.Ordinal)
                           || name.StartsWith("ps-", StringComparison.Ordinal)
                           || name.StartsWith("ps1", StringComparison.Ordinal)
                           || name.StartsWith("psone", StringComparison.Ordinal);

        return looksLikePs1 && file.Length >= 128L * 1024 && file.Length <= 2L * 1024 * 1024;
    }
}
