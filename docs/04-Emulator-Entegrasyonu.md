# 04 — Emülatör Entegrasyonu (CLI) ve Adapter Tasarımı

> **İlke:** GameShelf emülatörleri yalnızca `Process.Start` ile, **resmî komut satırı
> seçenekleriyle** başlatır. Emülatör binary'si uygulamaya gömülmez, indirilmez, güncellenmez.
> Kullanıcı kendi kurulumunun `exe` yolunu verir. Kullanılmayan/şüpheli bayraklar eklenmez
> (ör. RPCS3 `--decrypt` gibi şifre çözme araçları **asla** çağrılmaz — bkz. `LEGAL.md`).

## 1. Ortak sözleşme

`Domain/Interfaces/IEmulatorBackend.cs`:

```csharp
public interface IEmulatorBackend
{
    string Id { get; }                              // "duckstation" | "pcsx2" | "rpcs3" | plugin id
    PlatformId PlatformId { get; }
    string DisplayName { get; }
    IReadOnlyList<string> SupportedExtensions { get; }  // ".cue",".iso",".chd",...
    bool SupportsDirectoryTargets { get; }          // PS3 için true
    string DefaultArgumentTemplate { get; }         // kullanıcı değiştirebilir
    string FullscreenArgument { get; }              // {fullscreen} token karşılığı
    string NoFullscreenArgument { get; }
    bool RequiresBios { get; }
    string BiosHint { get; }

    string BuildArguments(EmulatorLaunchContext context);
    IEnumerable<LaunchIssue> Validate(EmulatorLaunchContext context);
}
```

**Şablon token'ları** (`EmulatorBackendBase.BuildArguments`):

| Token | Açılımı |
|---|---|
| `{game}` | Oyun dosyasının yolu (PS3'te `EBOOT.BIN` veya oyun klasörü). **Boşluk içeriyorsa otomatik tırnaklanır**; varsayılan şablonlar kendi tırnaklarını da içerir (`"{game}"`) |
| `{fullscreen}` | `FullscreenArgument` (tam ekran açıksa) / `NoFullscreenArgument` (kapalıysa) |
| `{extra}` | Game override → emulator config `ExtraArguments` sırasıyla; şablonda yoksa sona eklenir |
| `{bios}` | Ayarlardaki BIOS/firmware yolu (tırnaklı); tanımlı değilse boş |

Kurallar: token yoksa ek argüman sona eklenir, fazla boşluklar tek boşluğa indirilir,
`Trim()` uygulanır. **Kullanıcı şablonu her zaman kazanır** (boş bırakılırsa varsayılan döner).

## 2. Platform bazlı varsayılanlar

### 2.1 PS1 — DuckStation

```
varsayılan şablon : -batch {fullscreen} {extra} -- "{game}"
fullscreen        : -fullscreen      /  (kapalı: boş)
desteklenen       : .cue .bin .chd .iso .pbp .m3u .ecm .mds .ccd (DuckStation'ın okuduğu formatlar)
bios              : kullanıcı yolu (örn. D:\Emu\DuckStation\bios) — sadece varlık kontrolü
not               : Qt build'inde oyun kapanınca uygulamanın da kapanması için -batch
                    (SDL build'inde gerekmez). Big Picture isteyen kullanıcı {extra} ile
                    -bigpicture ekleyebilir.
```
Bilinen yararlı bayraklar (resmî): `-batch`, `-nogui`, `-fullscreen`, `-portable`,
`-settings <dosya>`, `-fastboot`, `-state <n>`, `-logfile <dosya>`.

### 2.2 PS2 — PCSX2 (Qt)

```
varsayılan şablon : {fullscreen} {extra} -- "{game}"
fullscreen        : --fullscreen     /  --nofullscreen
desteklenen       : .iso .chd .cso .bin .cue  (PCSX2'nin okuduğu formatlar)
bios              : kullanıcının PCSX2 bios klasörü — sadece varlık kontrolü
not               : `--` ayırıcısı, yolun tire ile başlaması/boşluk içermesi durumlarını güvenli kılar.
```
Bilinen yararlı bayraklar (resmî, PCSX2 wiki): `-batch`, `-nogui`, `-fullscreen`,
`-nofullscreen`, `-fastboot`, `-slowboot`, `-bios`, `-state <n>`, `-statefile <dosya>`,
`-bigpicture`, `-gameargs="..."`, `--` .

### 2.3 PS3 — RPCS3

```
varsayılan şablon : --no-gui {extra} "{game}"
fullscreen        : (boş — RPCS3 tam ekranı kendi config'inden yönetir;
                     kullandığın sürüm destekliyorsa şablona --fullscreen ekle)
desteklenen       : klasör hedefleri (PS3_GAME\USRDIR\EBOOT.BIN, PS3_DISC.SFB, PARAM.SFO)
                    + ileri seviye kullanıcı için .iso yolu (RPCS3 sürümüne göre değişir)
bios              : dev_flash / PS3 firmware yolu — sadece varlık kontrolü
not               : RPCS3, oyunun kendi arayüzünü açmadan direkt oyuna girmek için --no-gui
                    kullanır; ayrıca EBOOT.BIN doğrudan hedef verilebilir.
```
Bilinen yararlı bayraklar: `--no-gui`, `--headless`, oyun yolu (serbest argüman).
**Kullanılmayacaklar:** `--decrypt`, `--installpkg`/PKG kurulumu, RAP/license işlemleri
(DRM/licensing ile ilgili her şey kapsam dışı).

### 2.4 PS4 / PS5

Backend yok. `Platforms.IsSupported = 0`. UI'da:

> **PS4 / PS5 desteklenmiyor.** GameShelf emülasyon sağlamaz. İleride bir `IEmulatorBackend`
> plugin'i ile bu platform eklenebilir; plugin de emülasyon çekirdeği dağıtamaz.

## 3. Platform tahmini (`PlatformDetector`)

Sıra: **dizin yapısı → magic byte → uzantı → klasör ipucu → Unknown (kullanıcı düzeltir)**.

| Kontrol | Sonuç |
|---|---|
| Dizin içinde `PS3_GAME\USRDIR\EBOOT.BIN` veya `PS3_DISC.SFB` veya `PARAM.SFO` var | **PS3** (yüksek güven) |
| Dosya başında `MComprHD` (CHD) | PS1/PS2 belirsiz → klasör ipucu → yoksa **Unknown** (kullanıcı seçer) |
| `.cue` içinde `FILE "*.bin" BINARY` | **PS1** (yüksek) |
| ISO (`CD001` @ offset 0x8001) içinde ilk ~4 MB'da `PS3_DISC.SFB` | **PS3** |
| ISO içinde `SYSTEM.CNF` + `BOOT2 = ...` | **PS2** (yüksek) |
| ISO içinde `SYSTEM.CNF` + `BOOT = cdrom:\...` | **PS1** |
| `.pbp` | **PS1** |
| Sadece uzantı eşleşmesi | düşük güven → **Unknown** + kullanıcıya sorulur |

Uygulama notu: ISO içi tarama ham byte aramasıdır (ISO9660 parser'ı yok), bu yüzden
"ilk 4 MB'da ASCII marker ara" yaklaşımı kullanılır; hızlı ve yeterlidir.
Bulunamayan her durum kullanıcıya sorulur — **asla zorla platform atanmaz**.

## 4. Başlatma öncesi kontroller (`EmulatorLaunchService.ValidateAsync`)

| # | Kontrol | Sonuç |
|---|---|---|
| 1 | Platform destekli mi (`IsSupported`) | Error |
| 2 | `EmulatorConfigs.ExecutablePath` boş mu / dosya yok mu | Error |
| 3 | Oyun `FilePath` boş mu / diskte yok mu | Error (`IsMissing = 1` işaretlenir) |
| 4 | Uzantı backend'in desteklediği listede değil | Warning |
| 5 | Klasör hedefi ama backend dizin desteklemiyor (veya tersi) | Error |
| 6 | `RequiresBios` ve BIOS yolu ayarlanmamış / bulunamıyor | **Warning** (engelleyici değil) |
| 7 | Emülatör zaten çalışıyor mu (aynı oyun) | Warning |

Error varsa başlatma engellenir ve Settings'e götüren bir buton gösterilir.
Warning'ler log'a yazılır, kullanıcı onaylarsa devam edilir.

## 5. Process başlatma

```csharp
var psi = new ProcessStartInfo {
    FileName = config.ExecutablePath,
    Arguments = backend.BuildArguments(context),
    WorkingDirectory = string.IsNullOrWhiteSpace(config.WorkingDirectory)
        ? Path.GetDirectoryName(config.ExecutablePath) ?? string.Empty
        : config.WorkingDirectory,
    UseShellExecute = false,
    CreateNoWindow = false
};
```

- `UseShellExecute = false` (yol/dosya ilişkilendirmesi farklılıklarını önler).
- Başlatma sonrası: `LaunchHistory` satırı açılır, arka planda `WaitForExitAsync` ile
  süre ölçülür, çıkışta `Games.PlayTimeMinutes += dakika` ve `LastPlayedAt` güncellenir.
- `LaunchHistory.Arguments` tam komut satırını saklar → destek/sorun giderme için altın değer.

## 6. Sürüm farklarına dayanıklılık

Emülatörler CLI bayraklarını değiştirebilir. Bu yüzden:

1. Her backend'in varsayılan şablonu **Settings'te düzenlenebilir** + "Varsayılanа sıfırla" butonu.
2. Launch hatasında dialog: _"Emülatör başlatılamadı (çıkış kodu X). Argüman şablonunu
   kontrol edin."_ + log dosyasını açma linki.
3. Tools → Launch log, son argümanları gösterir; kullanıcı kendi emülatörünün `--help`
   çıktısıyla karşılaştırıp düzeltir.
