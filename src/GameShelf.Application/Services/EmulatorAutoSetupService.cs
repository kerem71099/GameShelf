using GameShelf.Application.Abstractions;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Extensions;

namespace GameShelf.Application.Services;

/// <summary>Otomatik kurulum raporu (kullanıcıya gösterilen özet).</summary>
public sealed class AutoSetupReport
{
    public List<string> Lines { get; } = new();

    /// <summary>En az bir yol bulunup kaydedildi mi?</summary>
    public bool Changed { get; set; }

    public string Summary =>
        Lines.Count == 0
            ? "Otomatik kurulum: yeni bir şey bulunamadı (emülatör veya BIOS klasörü diskte yok)."
            : string.Join(Environment.NewLine, Lines);
}

/// <summary>
/// Emülatör exe'sini ve BIOS/firmware klasörünü kullanıcının yerine BULUR ve kaydeder.
/// "Kullanıcı uğraşmasın" ilkesi: açılışta ve her başlatmadan önce sessizce çalışır.
/// <para>
/// YASAL SINIR: hiçbir ikili dosya indirilmez, kopyalanmaz, üretilmez. Yalnızca
/// kullanıcının diskinde zaten kurulu olan emülatör ve BIOS klasörünün YOLU kaydedilir.
/// </para>
/// </summary>
public sealed class EmulatorAutoSetupService
{
    private const string Category = nameof(EmulatorAutoSetupService);

    private static readonly PlatformId[] Platforms = { PlatformId.Ps1, PlatformId.Ps2, PlatformId.Ps3 };

    private readonly ILibraryRepository _repository;
    private readonly ISettingsService _settings;
    private readonly EmulatorSetupService _emulatorSetup;
    private readonly BiosCheckService _biosCheck;
    private readonly ILoggingService _logger;

    public EmulatorAutoSetupService(
        ILibraryRepository repository,
        ISettingsService settings,
        EmulatorSetupService emulatorSetup,
        BiosCheckService biosCheck,
        ILoggingService logger)
    {
        _repository = repository;
        _settings = settings;
        _emulatorSetup = emulatorSetup;
        _biosCheck = biosCheck;
        _logger = logger;
    }

    /// <summary>Tüm platformlar için eksikleri tamamlar (açılışta sessizce çalışır).</summary>
    public async Task<AutoSetupReport> RunAsync(CancellationToken cancellationToken = default)
        => await RunAsync(force: false, cancellationToken).ConfigureAwait(false);

    /// <summary>Kullanıcı düğmeye bastığında: kayıtlı yol bozuksa bile yeniden arar.</summary>
    public async Task<AutoSetupReport> RunForcedAsync(CancellationToken cancellationToken = default)
        => await RunAsync(force: true, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Başlatmadan hemen önce tek platform için hızlı kontrol.
    /// Emülatör yolu ve BIOS yolu yerindeyse neredeyse hiç disk okuması yapmaz.
    /// </summary>
    public async Task<bool> EnsurePlatformAsync(PlatformId platformId, CancellationToken cancellationToken = default)
    {
        if (platformId is not (PlatformId.Ps1 or PlatformId.Ps2 or PlatformId.Ps3))
        {
            return false;
        }

        var report = new AutoSetupReport();

        try
        {
            await SetupPlatformAsync(platformId, report, force: false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(Category, $"{platformId} başlatma öncesi otomatik kurulum başarısız.", ex);
            return false;
        }

        return report.Changed;
    }

    private async Task<AutoSetupReport> RunAsync(bool force, CancellationToken cancellationToken)
    {
        var report = new AutoSetupReport();

        foreach (var platform in Platforms)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await SetupPlatformAsync(platform, report, force, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(Category, $"{platform} otomatik kurulumu başarısız.", ex);
            }
        }

        return report;
    }

    private async Task SetupPlatformAsync(
        PlatformId platform,
        AutoSetupReport report,
        bool force,
        CancellationToken cancellationToken)
    {
        var key = platform.ToString();
        var label = platform.ToShortName();

        // 1) Emülatör exe: yoksa/bozuksa bilinen konumlarda ara ve kaydet.
        var config = await _repository.GetEmulatorConfigAsync(platform, cancellationToken).ConfigureAwait(false);
        var exePath = config?.ExecutablePath;

        if (force || string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            var found = await Task.Run(() => _emulatorSetup.DetectExecutable(key), cancellationToken).ConfigureAwait(false);

            if (found is not null && !string.Equals(found, exePath, StringComparison.OrdinalIgnoreCase))
            {
                await _emulatorSetup.SaveExecutablePathAsync(key, found, cancellationToken).ConfigureAwait(false);
                exePath = found;
                report.Changed = true;
                report.Lines.Add($"{label} emülatörü bulundu: {found}");
                _logger.Info(Category, $"{label} emülatörü otomatik bulundu: {found}");
            }
        }

        // 2) BIOS/firmware klasörü: kullanıcının diskinde varsa yolunu işaretle (dosya sağlamayız).
        _settings.Current.BiosPaths.TryGetValue(key, out var biosPath);
        var biosValid = !string.IsNullOrWhiteSpace(biosPath)
                        && (Directory.Exists(biosPath!) || File.Exists(biosPath!));

        if (force || !biosValid)
        {
            var detected = await Task.Run(() => BiosLocator.Find(platform, exePath), cancellationToken).ConfigureAwait(false);
            var target = BiosLocator.RecommendedDirectory(platform, exePath);

            if (detected is not null && !string.Equals(detected, biosPath, StringComparison.OrdinalIgnoreCase))
            {
                await _biosCheck.SetBiosPathAsync(platform, detected, cancellationToken).ConfigureAwait(false);
                report.Changed = true;
                report.Lines.Add($"{label} BIOS klasörü bulundu: {detected}");
                _logger.Info(Category, $"{label} BIOS klasörü otomatik bulundu: {detected}");

                if (target is not null && !string.Equals(detected, target, StringComparison.OrdinalIgnoreCase))
                {
                    report.Lines.Add(
                        $"{label}: emülatörün okuduğu klasör farklı → {target} " +
                        "(Araçlar → BIOS → Klasörü aç)");
                }
            }
            else if (detected is null && !biosValid)
            {
                report.Lines.Add(
                    $"{label} BIOS'u bulunamadı → kendi BIOS'unu şu klasöre koy: {target} " +
                    "(Araçlar → BIOS → Klasörü aç)");
                _logger.Warning(Category, $"{label} BIOS klasörü bulunamadı (beklenen: {target}).");
            }
        }
    }
}
