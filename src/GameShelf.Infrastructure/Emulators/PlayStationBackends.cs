using GameShelf.Domain.Enums;
using GameShelf.Domain.Models;

namespace GameShelf.Infrastructure.Emulators;

/// <summary>
/// PS1 — DuckStation.
/// Resmî CLI'den yalnızca güvenli/belgelenmiş bayraklar kullanılır:
/// -batch (oyun kapanınca çık), -fullscreen, -fastboot gibi ekstralar kullanıcı şablonuna bırakılır.
/// </summary>
public sealed class DuckStationBackend : EmulatorBackendBase
{
    public override string Id => "duckstation";

    public override PlatformId PlatformId => PlatformId.Ps1;

    public override string DisplayName => "DuckStation";

    public override IReadOnlyList<string> SupportedExtensions { get; } =
        [".cue", ".bin", ".iso", ".chd", ".pbp", ".m3u", ".ecm", ".mds", ".ccd"];

    public override string DefaultArgumentTemplate => "-batch {fullscreen} {extra} -- \"{game}\"";

    public override string FullscreenArgument => "-fullscreen";

    public override bool RequiresBios => true;

    public override string BiosHint =>
        "DuckStation'ın BIOS klasörünü seçin (örn. scph1001.bin). BIOS'u kendi konsolunuzdan dump etmelisiniz.";
}

/// <summary>
/// PS2 — PCSX2 (Qt).
/// Resmî CLI (pcsx2.net/docs/advanced/cli): bayraklar TEK TİRE ile yazılır:
/// -batch, -nogui, -fullscreen, -nofullscreen, -bigpicture, -state, -disc ...
/// "--" ayırıcısı: dosya adı boşluk içeriyorsa/tire ile başlıyorsa kullanılır.
/// Eski (wxWidgets) sürümlerin "--fullscreen" yazımı Qt sürümünde
/// "Unknown parameter" hatası verir — bu yüzden çift tire KULLANILMIYOR.
/// </summary>
public sealed class Pcsx2Backend : EmulatorBackendBase
{
    public override string Id => "pcsx2";

    public override PlatformId PlatformId => PlatformId.Ps2;

    public override string DisplayName => "PCSX2";

    public override IReadOnlyList<string> SupportedExtensions { get; } =
        [".iso", ".chd", ".cso", ".bin", ".cue", ".mdf"];

    public override string DefaultArgumentTemplate => "{fullscreen} {extra} -- \"{game}\"";

    public override string FullscreenArgument => "-fullscreen";

    public override string NoFullscreenArgument => "-nofullscreen";

    public override bool RequiresBios => true;

    public override string BiosHint =>
        "PCSX2'nin 'bios' klasörünü seçin. BIOS'u kendi konsolunuzdan dump etmelisiniz.";

    /// <summary>
    /// Kullanıcı eski (wxWidgets) yazımı bir şablona kaydettiyse Qt sürümü
    /// "Unknown parameter" hatası verir; bilinen bayrakları tek tireye çeviririz.
    /// </summary>
    public override string BuildArguments(EmulatorLaunchContext context)
    {
        var arguments = base.BuildArguments(context);

        return arguments
            .Replace("--fullscreen", "-fullscreen", StringComparison.Ordinal)
            .Replace("--nofullscreen", "-nofullscreen", StringComparison.Ordinal)
            .Replace("--nogui", "-nogui", StringComparison.Ordinal)
            .Replace("--batch", "-batch", StringComparison.Ordinal);
    }
}

/// <summary>
/// PS3 — RPCS3.
/// --no-gui ile emülatör arayüzü açılmadan doğrudan oyuna girilir.
/// Hedef: oyun klasörü (PS3_GAME\USRDIR\EBOOT.BIN) veya kullanıcının verdiği ISO yolu.
/// NOT: --decrypt / PKG kurulum / RAP gibi DRM-license işlemleri KULLANILMAZ.
/// </summary>
public sealed class Rpcs3Backend : EmulatorBackendBase
{
    public override string Id => "rpcs3";

    public override PlatformId PlatformId => PlatformId.Ps3;

    public override string DisplayName => "RPCS3";

    public override IReadOnlyList<string> SupportedExtensions { get; } = [".iso", ".bin"];

    public override bool SupportsDirectoryTargets { get; } = true;

    public override string DefaultArgumentTemplate => "--no-gui {extra} \"{game}\"";

    // RPCS3 tam ekranı kendi yapılandırmasından yönetir; sürüm destekliyorsa
    // kullanıcı şablona --fullscreen ekleyebilir.
    public override string FullscreenArgument => string.Empty;

    public override string NoFullscreenArgument => string.Empty;

    public override bool RequiresBios => true;

    public override string BiosHint =>
        "RPCS3'ün PS3 firmware (dev_flash) yolunu seçin. Firmware'i kendi konsolunuzdan almalısınız.";
}
