# 09 · Tek EXE, emülatör kurulumu ve oyun klasörü

Bu belge üç soruyu yanıtlar:

1. Tek EXE nasıl üretilir?
2. Emülatörler nasıl hazırlanır (uygulamaya "birlikte gelmesi" için pratik yol)?
3. Kendi oyun klasörüm neresi?

---

## 1) Tek EXE üretmek

```bat
scripts\publish-exe.bat
```

Seçenekler:

| Seçim | Boyut | Gereksinim |
|---|---|---|
| **1) Self-contained** (önerilen) | ~70 MB | Hiçbir şey gerekmez; temiz bir Windows 10/11 PC'de çalışır |
| 2) Framework-dependent | ~10 MB | Hedef PC'de .NET 8 **Desktop Runtime** kurulu olmalı |

Çıktı:

```
dist\GameShelf.exe
dist\emulators\        <- emülatörleri buraya koyabilirsin (opsiyonel)
```

> **Test:** EXE'yi mutlaka `dist\` dışındaki boş bir klasöre kopyalayıp oradan çalıştır.
> SQLite native kütüphanesi (`e_sqlite3.dll`) tek dosyadan kendini açar; ilk açılış 1-2 saniye sürebilir.

İstersen elle:

```bat
dotnet publish src\GameShelf.App\GameShelf.App.csproj -c Release -r win-x64 ^
  -p:SelfContained=true -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

---

## 2) Emülatörler

### GameShelf emülatör DAĞITMAZ

DuckStation, PCSX2 ve RPCS3 ayrı projelerdir (GPL lisanslı), sık güncellenir ve onlarca MB boyutundadır.
Bu yüzden:

- Repoda/EXE içinde emülatör ikili dosyası **yoktur**.
- Uygulama yalnızca **resmî indirme sayfasını açar** (`Araçlar → KURULUM → İndir`).
- Aynı şekilde **BIOS/firmware/anahtar da dağıtılmaz**; kullanıcı kendi konsolundan sağlar.

### Otomatik kurulum: emülatör + BIOS'u uygulama kendisi bulur

Kullanıcı hiçbir yolu elle seçmesin diye GameShelf açılışta — ve her oyun
başlatılmadan hemen önce — şunları **kendisi** arar:

1. **Emülatör exe'si:** `%LOCALAPPDATA%\GameShelf\emulators\...`, EXE'nin yanındaki
   `emulators\`, `Program Files` ve diğer bilinen konumlar.
2. **BIOS/firmware klasörü:** emülatörün kendi `bios` klasörü (exe'nin yanı),
   `Belgelerim\PCSX2\bios`, `Belgelerim\DuckStation\bios`, `%USERPROFILE%\RPCS3\dev_flash`
   gibi konumlar. Dosya gerçekten oradaysa (PS2 için 4 MB `*.bin`, PS1 için 512 KB `*.bin`,
   PS3 için `dev_flash` ağacı) yol otomatik kaydedilir.

Bulunan her şey `Ayarlar → EMÜLATÖRLER / BIOS YOLLARI` ve `Araçlar → KURULUM / BIOS`
ekranlarında görünür. **Araçlar → "⚡ Otomatik kurulum"** düğmesi aynı taramayı elle
(kayıtlı yol bozuksa da) yeniden yapar.

> **Yasal sınır:** GameShelf BIOS indirmez, kopyalamaz, üretmez. Yalnızca senin diskinde
> zaten var olan dosyanın **yolunu** işaretler. BIOS'u kendi konsolundan dump etmelisin.

- **BIOS'un yoksa:** `Araçlar → BIOS / FIRMWARE KONTROLÜ` satırındaki **"Klasörü aç"**
  düğmesi emülatörün BIOS'u aradığı klasörü açar (gerekirse oluşturur). Kendi BIOS'unu
  oraya sürükle, sonra **"⚡ Otomatik kurulum"** ile yenile.
- **PCSX2 hangi klasörü okur?** Kurulum tipine göre değişir: `portable.ini` exe'nin
  yanındaysa **exe'nin yanındaki `bios`**, yoksa **`Belgelerim\PCSX2\bios`**. GameShelf bu
  ayrımı kendisi yapar; "Klasörü aç" düğmesi PCSX2'nin gerçekten okuduğu klasörü açar.
- **Emülatör hemen kapanırsa:** emülatörün konsola yazdığı mesaj artık log'a yazılır ve
  ekranda gösterilir (`Unknown parameter: ...` gibi). Emülatör mesaj yazmazsa log'a bak:
  `%LOCALAPPDATA%\GameShelf\logs\app-YYYY-AA-GG.log`.
- **Hangi derlemeyi çalıştırdığın:** `Ayarlar → GELİŞMİŞ → Sürüm` satırında yazar
  (`v0.1.0 · 04.10.2026 15:30` gibi). Destek isterken bu satırı ilet.

### Pratik çözüm: `emulators\` klasörü

EXE'nin yanına bir `emulators\` klasörü koy ve emülatörleri oraya çıkar:

```
GameShelf.exe
emulators\
    PS1\duckstation-qt-x64-ReleaseLTCG.exe
    PS2\pcsx2-qtx64-avx2.exe
    PS3\rpcs3.exe
```

Uygulama açılırken bu klasöre bakar: `Araçlar → KURULUM → **Otomatik ara**` düğmesi
emülatörü bulur ve yolu kaydeder. Böylece "uygulamayla birlikte gelmiş" gibi olur;
tek yapman gereken klasörü EXE ile birlikte taşımak.

### Uygulama içinden (önerilen): İndirmeler sekmesi

Sol gezinmede **İndirmeler** sekmesi var. Her kalem için "**İndir ve kur**":

| Kalem | Kategori | Not |
|---|---|---|
| DuckStation (PS1) | Emülatör | zip, uygulama kendisi açar |
| PCSX2 (PS2) | Emülatör | 7z — 7-Zip gerekir |
| RPCS3 (PS3) | Emülatör | 7z — 7-Zip gerekir |
| 7-Zip | Araç | `.7z` dosyalarını açmak için; listeden indirilip kurulur |
| .NET 8 Desktop Runtime | Araç | Yalnızca framework-dependent EXE için |

İndirilenler `%LOCALAPPDATA%\GameShelf\emulators\<PS1|PS2|PS3>` klasörüne açılır ve
emülatör yolu otomatik kaydedilir. Betik/komut satırı gerekmez.

### Hazır indirme betiği

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\emulatorleri-indir.ps1
```

- Resmî GitHub sürümlerini (en son sürüm) indirir → `emulators\PS1|PS2|PS3\`.
- `.zip` dosyalarını kendisi açar; `.7z` için **7-Zip** gerekebilir (yoksa indirme
  sayfasını tarayıcıda açar, elle kurarsın).
- Betik yalnızca emülatör programını indirir; **oyun/BIOS indirmez**.

### Elle kurulum (garanti yol)

`Araçlar → KURULUM` bölümünde her platform için üç düğme var:

| Düğme | Ne yapar |
|---|---|
| **İndir** | Emülatörün resmî indirme sayfasını tarayıcıda açar |
| **Otomatik ara** | `emulators\`, Program Files, `%LOCALAPPDATA%\Programs` içinde arar, bulunca kaydeder |
| **Gözat** | İndirdiğin `.exe`'yi elle seçersin |

Ayrıca `Ayarlar → Emülatörler` ekranından da yol gösterebilirsin.

| Platform | Emülatör | Resmî adres |
|---|---|---|
| PS1 | DuckStation | <https://github.com/stenzek/duckstation/releases/latest> |
| PS2 | PCSX2 | <https://github.com/PCSX2/pcsx2/releases/latest> |
| PS3 | RPCS3 | <https://github.com/RPCS3/rpcs3/releases/latest> |
| PS4 / PS5 | — | Yasal emülatör yok; uygulamada yalnızca bilgi kartı |

---

## 3) Kendi oyun klasörü

İlk açılışta uygulama şu klasörü oluşturur ve kütüphaneye ekler:

```
C:\Users\<kullanıcı>\GameShelf\Games
```

- Oyun dosyalarını (`.iso`, `.chd`, `.cue/.bin`, PS3 klasörleri) buraya at.
- `Araçlar → Yeniden tara` (veya F5) ile taranır.
- `Araçlar → KURULUM → Klasörü aç` ile klasöre gidersin.
- İstersen `Ayarlar → Kütüphane klasörleri`'nden başka klasörler de ekleyebilirsin.

> Klasör yalnızca **hiç kütüphane klasörü yokken** otomatik eklenir; sonra eklediğin
> klasörlere dokunulmaz.

---

## Yasal özet

- GameShelf: emülatör, BIOS/firmware, anahtar, ROM/ISO **sağlamaz, indirmez, önermez**.
- Yalnızca kullanıcının kendi yasal yedeklerini düzenler ve kullanıcının seçtiği
  emülatöre komut satırı argümanı olarak geçirir.
- Ayrıntılar: [`../LEGAL.md`](../LEGAL.md).
