using GameShelf.Domain.Enums;
using GameShelf.Domain.Extensions;
using GameShelf.Domain.Interfaces;
using GameShelf.Domain.Models;

namespace GameShelf.Infrastructure.Emulators;

/// <summary>
/// Tüm emülatör adapter'larının ortak davranışı:
/// argüman üretimi (şablon token'ları) + başlatma öncesi kontroller.
/// <para>Bu sınıf emülatör çekirdeği içermez; yalnızca komut satırı üretir.</para>
/// </summary>
public abstract class EmulatorBackendBase : IEmulatorBackend
{
    public abstract string Id { get; }

    public abstract PlatformId PlatformId { get; }

    public abstract string DisplayName { get; }

    public abstract IReadOnlyList<string> SupportedExtensions { get; }

    public virtual bool SupportsDirectoryTargets { get; }

    public abstract string DefaultArgumentTemplate { get; }

    public virtual string FullscreenArgument => string.Empty;

    public virtual string NoFullscreenArgument => string.Empty;

    public virtual bool RequiresBios => true;

    public virtual string BiosHint =>
        "BIOS/firmware dosyalarını kendi konsolunuzdan dump etmeniz gerekir. GameShelf bunları sağlamaz.";

    public virtual string BuildArguments(EmulatorLaunchContext context)
    {
        var template = string.IsNullOrWhiteSpace(context.GameOverride?.ArgumentTemplate)
            ? string.IsNullOrWhiteSpace(context.Emulator.ArgumentTemplate)
                ? DefaultArgumentTemplate
                : context.Emulator.ArgumentTemplate
            : context.GameOverride!.ArgumentTemplate;

        return EmulatorArgumentRenderer.Render(template, context, FullscreenArgument, NoFullscreenArgument);
    }

    public virtual IEnumerable<LaunchIssue> Validate(EmulatorLaunchContext context)
    {
        // Uzantı uyarısı (engelleyici değil: kullanıcı bilinçli olarak farklı format kullanabilir)
        if (!SupportsDirectoryTargets
            && File.Exists(context.Game.FilePath)
            && !SupportedExtensions.Contains(Path.GetExtension(context.Game.FilePath), StringComparer.OrdinalIgnoreCase))
        {
            yield return LaunchIssue.Warning("extension_unexpected",
                $"{Path.GetExtension(context.Game.FilePath)} bu emülatör için beklenen bir uzantı değil " +
                $"({string.Join(", ", SupportedExtensions)}).");
        }

        // BIOS: yalnızca varlık kontrolü — dosya asla sağlanmaz/indirilmez.
        if (RequiresBios)
        {
            if (string.IsNullOrWhiteSpace(context.BiosPath))
            {
                yield return LaunchIssue.Warning("bios_not_configured",
                    $"{PlatformId.ToShortName()} için BIOS/firmware yolu ayarlanmamış. " +
                    "Emülatör kendi ayarlarını kullanabilir; gerekirse Ayarlar → Emülatörler'den yol verin.");
            }
            else if (!Directory.Exists(context.BiosPath) && !File.Exists(context.BiosPath))
            {
                yield return LaunchIssue.Warning("bios_path_missing",
                    $"BIOS/firmware yolu bulunamadı: {context.BiosPath}");
            }
        }
    }
}
