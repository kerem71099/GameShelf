using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Domain.Enums;

namespace GameShelf.Application.Services;

/// <summary>
/// BIOS/firmware yolu kontrolü.
/// GameShelf bu dosyaları SAĞLAMAZ, indirmez, kopyalamaz; yalnızca kullanıcının
/// ayarlarda verdiği yolun varlığını kontrol eder ve uyarı üretir.
/// </summary>
public sealed class BiosCheckService
{
    private const string LegalNote =
        "BIOS/firmware dosyalarını kendi konsolunuzdan dump etmeniz gerekir. GameShelf bu dosyaları sağlamaz.";

    private readonly ILibraryRepository _repository;
    private readonly ISettingsService _settings;

    public BiosCheckService(ILibraryRepository repository, ISettingsService settings)
    {
        _repository = repository;
        _settings = settings;
    }

    public static string Notice => LegalNote;

    public async Task<IReadOnlyList<BiosStatus>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var platforms = await _repository.GetPlatformsAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<BiosStatus>();

        foreach (var platform in platforms
                     .Where(p => p.Id is PlatformId.Ps1 or PlatformId.Ps2 or PlatformId.Ps3)
                     .OrderBy(p => p.SortOrder))
        {
            _settings.Current.BiosPaths.TryGetValue(platform.Id.ToString(), out var path);
            var configured = !string.IsNullOrWhiteSpace(path);
            var exists = configured && (Directory.Exists(path!) || File.Exists(path!));

            rows.Add(new BiosStatus(
                platform.Id,
                platform.ShortName,
                platform.RequiresBios,
                configured,
                exists,
                configured ? path : null,
                HintFor(platform.Id)));
        }

        return rows;
    }

    public async Task SetBiosPathAsync(PlatformId platformId, string? path, CancellationToken cancellationToken = default)
    {
        var key = platformId.ToString();

        await _settings.UpdateAsync(settings =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                settings.BiosPaths.Remove(key);
            }
            else
            {
                settings.BiosPaths[key] = path;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string HintFor(PlatformId platformId) => platformId switch
    {
        PlatformId.Ps1 => "DuckStation'ın BIOS klasörünü seçin (örn. scph1001.bin dosyasının bulunduğu klasör). " + LegalNote,
        PlatformId.Ps2 => "PCSX2'nin 'bios' klasörünü seçin. " + LegalNote,
        PlatformId.Ps3 => "RPCS3'ün PS3 firmware (dev_flash) yolunu seçin. " + LegalNote,
        _ => LegalNote
    };
}
