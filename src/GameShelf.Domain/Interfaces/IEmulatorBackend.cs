using GameShelf.Domain.Enums;
using GameShelf.Domain.Models;

namespace GameShelf.Domain.Interfaces;

/// <summary>
/// Bir platformun nasıl başlatılacağını bilen sözleşme.
/// <para>
/// BU ARAYÜZ emülatör çekirdeği içermez. Yalnızca: (1) komut satırı argümanı üretir,
/// (2) başlatma öncesi kontrol yapar. Uygulama emülatörü <c>Process.Start</c> ile açar.
/// </para>
/// </summary>
public interface IEmulatorBackend
{
    /// <summary>Benzersiz kimlik: "duckstation", "pcsx2", "rpcs3" veya plugin id'si.</summary>
    string Id { get; }

    PlatformId PlatformId { get; }

    string DisplayName { get; }

    /// <summary>Noktalı küçük harf uzantı listesi: ".cue", ".iso", ".chd".</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>PS3 gibi klasör yapılarını hedef olarak kabul eder mi?</summary>
    bool SupportsDirectoryTargets { get; }

    /// <summary>Token'ları destekleyen varsayılan argüman şablonu: {game} {fullscreen} {extra} {bios}.</summary>
    string DefaultArgumentTemplate { get; }

    /// <summary>{fullscreen} token'ının tam ekran AÇIKken karşılığı.</summary>
    string FullscreenArgument { get; }

    /// <summary>{fullscreen} token'ının tam ekran KAPALIyken karşılığı.</summary>
    string NoFullscreenArgument { get; }

    /// <summary>Bu platform için BIOS/firmware yolu gerekli mi? (yalnızca varlık kontrolü yapılır)</summary>
    bool RequiresBios { get; }

    /// <summary>Ayarlar ekranında gösterilen BIOS ipucu metni.</summary>
    string BiosHint { get; }

    /// <summary>Şablondan gerçek komut satırı argümanlarını üretir.</summary>
    string BuildArguments(EmulatorLaunchContext context);

    /// <summary>Platforma özel başlatma öncesi kontroller.</summary>
    IEnumerable<LaunchIssue> Validate(EmulatorLaunchContext context);
}
