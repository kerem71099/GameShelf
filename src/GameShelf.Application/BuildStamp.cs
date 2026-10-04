using System.Globalization;
using System.Reflection;

namespace GameShelf.Application;

/// <summary>
/// Çalışan derlemenin sürüm/damga bilgisi.
/// Amaç: destek sırasında "hangi derlemeyi çalıştırıyorsun?" sorusuna net cevap almak
/// (Ayarlar → Gelişmiş bölümünde ve açılış log'unda görünür).
/// </summary>
public static class BuildStamp
{
    /// <summary>Örn. "1.0.0" (git hash'i varsa atılır).</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>Derleme dosyasının son yazılma zamanı (gg.AA.yyyy ss:dd).</summary>
    public static string BuiltAt { get; } = ReadBuiltAt();

    public static string Display => $"v{Version} · {BuiltAt}";

    private static string ReadVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return assembly.GetName().Version?.ToString() ?? "dev";
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);

        return plus > 0 ? informational[..plus] : informational;
    }

    private static string ReadBuiltAt()
    {
        var location = Assembly.GetExecutingAssembly().Location;

        if (string.IsNullOrWhiteSpace(location))
        {
            // tek dosya (single-file) yayında Location boş olabilir
            location = Path.Combine(AppContext.BaseDirectory, "GameShelf.Application.dll");
        }

        if (!File.Exists(location))
        {
            // self-contained tek EXE: yan yana dll yoktur, ana EXE'nin zamanını kullan
            location = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.exe")
                .FirstOrDefault() ?? location;
        }

        return File.Exists(location)
            ? File.GetLastWriteTime(location).ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture)
            : "—";
    }
}
