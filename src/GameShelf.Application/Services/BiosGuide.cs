namespace GameShelf.Application.Services;

/// <summary>
/// BIOS/firmware edinme rehberi — YALNIZCA yasal yol anlatılır.
/// <para>
/// BIOS, konsolun kendi sistem yazılımıdır ve Sony'nin telifli ürünüdür.
/// Bu yüzden GameShelf (ve bu rehber) BIOS dosyası sağlamaz, indirmez,
/// indirme bağlantısı vermez. Tek yasal yol: sahip olunan konsoldan dump etmek.
/// </para>
/// </summary>
public static class BiosGuide
{
    /// <summary>PCSX2'nin resmî BIOS kurulum dokümanı (dosya değil, dokümantasyon).</summary>
    public const string OfficialGuideUrl = "https://pcsx2.net/docs/setup/bios";

    /// <summary>
    /// PS3 firmware'i Sony'nin resmî sistem yazılımı sayfasından indirilir.
    /// PS1/PS2'den FARKLI olarak PS3'te dump gerekmez: firmware resmî ve ücretsizdir.
    /// </summary>
    public const string Ps3FirmwareUrl = "https://www.playstation.com/tr-tr/support/hardware/ps3/system-software/";

    /// <summary>DuckStation proje sayfası (README'de BIOS gereksinimi açıklanır).</summary>
    public const string DuckStationGuideUrl = "https://github.com/stenzek/duckstation";

    public static IReadOnlyList<string> Steps { get; } =
    [
        "BIOS nedir? Konsolun kendi sistem yazılımıdır (PC açılırken gördüğün BIOS'un aynısı). Sony'nin telifli ürünüdür.",
        "Bu yüzden internetten indirilemez: oyun ROM'u/ISO indirmekle aynı şeydir. GameShelf sana böyle bir bağlantı veremez — hiçbir yasal emülatör de vermez.",
        "Tek yasal yol: sahip olduğun PS2 konsolundan BIOS'u kendin çıkarmak (buna 'dump' denir).",
        "Gerekenler: PS2 konsolu (fat/slim), FreeMcBoot bellek kartı (yoksa FreeDVDBoot veya modchip), FAT32 biçimli bir USB bellek, uLaunchELF ve biosdrain.elf programı.",
        "Adımlar: USB'yi FAT32 yap → biosdrain.elf'i USB'nin köküne kopyala → PS2'de uLaunchELF'i aç → mass:/ içinden biosdrain.elf'i çalıştır → 2-5 dakika bekle.",
        "Çıkan SCPH-XXXXX.bin dosyasını USB'den bu ekrandaki 'Klasörü aç' düğmesinin açtığı klasöre kopyala. Geçerli bir PS2 BIOS tam olarak 4.194.304 bayt (4 MB) olur.",
        "Önemli: emülatör SADECE kendi BIOS klasörünü okur. GameShelf'e yolu göstermek yetmez; dosyanın o klasörün İÇİNDE olması gerekir. 'Dosya seç' düğmesi dosyayı bulunduğu yerden seçmeni sağlar, gerekirse kaynak ve hedef klasörü yan yana açar.",
        "PS1 için de kural aynı (BIOS 512 KB). Not: DuckStation, PS2 BIOS'unu da kullanabilir — tek bir dump iki işi görür.",
        "PS3 FARKLI (iyi haber): firmware'i konsoldan dump etmen gerekmez. Sony'nin resmî PS3 sistem yazılımı sayfasından PS3UPDAT.PUP dosyasını indirip RPCS3'te 'File → Install Firmware' ile kurarsın — resmî ve ücretsizdir.",
        "Konsolun yok mu? İkinci el bir PS2 alıp BIOS'unu dump ettikten sonra geri satabilirsin. Bunun dışında yasal bir yol yok.",
    ];
}
