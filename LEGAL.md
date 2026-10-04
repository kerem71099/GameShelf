# Yasal & Etik Sınırlar (uygulamanın değiştirilemez çerçevesi)

Bu proje **yalnızca launcher/frontend**'dir. Aşağıdaki kurallar tasarımın ve kodun parçasıdır;
bir özellik bu listedeki herhangi bir maddeyi ihlal ediyorsa **yazılmayacaktır**.

## Yapılacaklar (izinli)

- Kullanıcının diskindeki, **kendi yasal olarak edindiği** oyun dump'larını taramak.
- Kullanıcının **kendi sağladığı** BIOS/firmware dosyalarının **yolunu** kaydetmek ve
  varlığını kontrol etmek (sadece `File.Exists` / `Directory.Exists`).
- Emülatörleri **resmî CLI seçenekleriyle** `Process.Start` ile başlatmak.
- Kapak görseli, başlık, region, not gibi metadata'yı **kullanıcıdan** alıp yerelde saklamak.
- Launch log'u, oynama süresi gibi **yerel** telemetri (internete çıkmaz).

## Yapılmayacaklar (yasak)

| Yasak | Açıklama |
|---|---|
| ROM/ISO indirme | Web'den oyun dosyası indirme, linkleme, yönlendirme yok. |
| Scraping | Herhangi bir online metadata/cover API'sine istek yok (uygulama offline). |
| Torrent / P2P | Magnet, torrent, DDL, dosya paylaşım ağı entegrasyonu yok. |
| Crack / serial / key | Lisans anahtarı üretme, doğrulama atlatma, keygen yok. |
| DRM / şifre kırma | ISO şifre çözme, PKG/RAP decrypt, disc key sağlama, firmware decrypt yok. |
| BIOS / firmware / keys dağıtımı | Uygulama BIOS, firmware, decryption key **içermez ve indirmez**; sadece kullanıcının verdiği yolu kullanır. |
| Emülatör dağıtımı | EXE içinde DuckStation/PCSX2/RPCS3 binary'si paketlenmez; kullanıcı kendi kurulumunun yolunu verir. |
| PS4/PS5 emülasyonu | Desteklenmez; UI placeholder + ileride plugin arayüzü (yine emülasyon/DRM bypass içeremez). |

## Uygulama içi görünürlük

- `Settings → Advanced` ve `Tools → BIOS Check` ekranlarında şu metin görünür:
  _"BIOS/firmware dosyalarını kendi konsolunuzdan dump etmeniz gerekir. GameShelf bu dosyaları sağlamaz."_
- Plugin manifest'i (`plugin.json`) `declaresNoDrmBypass: true` alanı zorunludur; `false` veya eksikse plugin reddedilir.
- Kod içinde "yasal uyarı" sabitleri: `Application/Resources/LegalTexts` (bkz. `docs/05-Plugin-Mimarisi.md`).

## Kullanıcı sorumluluğu

Kullanıcı, kütüphanesine eklediği dosyaların yasal sahipliğinden/kullanım hakkından sorumludur.
GameShelf bu içeriği **incelemez, doğrulamaz, kopyalamaz, paylaşmaz**; yalnızca yolu kaydeder ve
kullanıcının seçtiği emülatöre komut satırı argümanı olarak geçirir.
