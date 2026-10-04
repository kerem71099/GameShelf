using System.Globalization;
using System.Text.RegularExpressions;

namespace GameShelf.Application.Services;

/// <summary>
/// Dosya adından okunabilir oyun başlığı üretir.
/// Metadata offline olduğu için başlığın ilk hâli dosya adından türetilir;
/// kullanıcı Game Details ekranında düzeltir.
/// </summary>
public static partial class TitleCleaner
{
    [GeneratedRegex(@"\((?<tag>[^)]*)\)|\[(?<tag>[^\]]*)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BracketTagRegex();

    [GeneratedRegex(
        @"\b(rev\s*\d*|v\d+(\.\d+)*|usa|europe|japan|jpn|eu|us|jp|uk|de|fr|es|it|" +
        @"disc\s*\d*|cd\s*\d*|cd\d|dvd\d|npeb\d*|npub\d*|sces\d*|scus\d*|scps\d*|sles\d*|slus\d*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JunkTokenRegex();

    public static string Clean(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name))
        {
            name = path;
        }

        name = Path.GetFileNameWithoutExtension(name);

        // (Europe) [SCES-01234] gibi sahne etiketlerini at
        name = BracketTagRegex().Replace(name, " ");

        // Ayırıcıları boşluğa çevir
        name = name.Replace('_', ' ').Replace('.', ' ');

        // Region/rev/disk/serial gürültüsünü at
        name = JunkTokenRegex().Replace(name, " ");

        name = Regex.Replace(name, @"\s{2,}", " ").Trim();

        return string.IsNullOrWhiteSpace(name)
            ? Path.GetFileNameWithoutExtension(path)
            : name;
    }

    /// <summary>Sıralama için normalize başlık (küçük harf, baştaki artikeller atılır).</summary>
    public static string ToSortTitle(string title)
    {
        var value = (title ?? string.Empty).Trim().ToLowerInvariant();
        value = Regex.Replace(value, @"^(the|a|an)\s+", string.Empty);
        value = Regex.Replace(value, @"[^\p{L}\p{N} ]", string.Empty);
        return Regex.Replace(value, @"\s{2,}", " ").Trim();
    }

    /// <summary>Kapak yerine gösterilecek baş harfler (en fazla 2 harf).</summary>
    public static string Initials(string title)
    {
        var words = (title ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var text = words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)],
            _ => $"{char.ToUpper(words[0][0], CultureInfo.InvariantCulture)}{char.ToLower(words[1][0], CultureInfo.InvariantCulture)}"
        };

        return text.ToUpperInvariant();
    }
}
