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
