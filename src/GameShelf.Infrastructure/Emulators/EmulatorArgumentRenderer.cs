using System.Text.RegularExpressions;
using GameShelf.Domain.Models;

namespace GameShelf.Infrastructure.Emulators;

/// <summary>
/// Argüman şablonu → gerçek komut satırı.
/// Token'lar: {game} {fullscreen} {extra} {bios}
/// <para>
/// {game} ham yolu verir; şablonlar genelde kendi tırnaklarını içerir ("{game}").
/// Yol boşluk içeriyorsa ve şablonda tırnak yoksa otomatik tırnaklanır.
/// </para>
/// </summary>
public static partial class EmulatorArgumentRenderer
{
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpacesRegex();

    public static string Render(
        string template,
        EmulatorLaunchContext context,
        string fullscreenArgument,
        string noFullscreenArgument)
    {
        var extra = context.ExtraArguments ?? string.Empty;
        var gamePath = context.Game.FilePath;
        var biosPath = context.BiosPath;

        var fullscreen = context.Fullscreen ? fullscreenArgument : noFullscreenArgument;

        var rendered = template
            .Replace("{game}", QuoteIfNeeded(gamePath), StringComparison.OrdinalIgnoreCase)
            .Replace("{fullscreen}", fullscreen, StringComparison.OrdinalIgnoreCase)
            .Replace("{extra}", extra, StringComparison.OrdinalIgnoreCase)
            .Replace("{bios}", string.IsNullOrWhiteSpace(biosPath) ? string.Empty : QuoteIfNeeded(biosPath),
                StringComparison.OrdinalIgnoreCase);

        // Şablonda {extra} yoksa ek argümanları sona ekle (kullanıcı şablonu bozmasın)
        if (!template.Contains("{extra}", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(extra))
        {
            rendered += " " + extra;
        }

        return MultipleSpacesRegex().Replace(rendered, " ").Trim();
    }

    /// <summary>Boşluk içeriyorsa tırnaklar, aksi hâlde ham yol.</summary>
    private static string QuoteIfNeeded(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Contains(' ') ? $"\"{path}\"" : path;
    }
}
