# 08 — Kendi Bilgisayarında Çalıştırma (Windows 10/11)

Bu dosya, GameShelf'i kendi PC'nde **ilk kez** ayağa kaldırmak için gereken adımları anlatır.
Tahmini süre: **5–10 dakika** (indirmeler hariç).

> ⚠️ **En sık yapılan hata:** komut satırının başında `PS C:\WINDOWS\system32>` yazıyorsa
> yanlış klasördesin demektir. `PS C:\Users\<sen>\...\GameShelf>` gibi **proje klasörünü**
> görmelisin. PowerShell'i proje klasöründe açmak için: klasörü Explorer'da aç → adres
> çubuğuna `powershell` yaz → Enter.

---

## 1. Gereksinimler

| Bileşen | Zorunlu mu? | Nereden |
|---|---|---|
| **.NET 8 SDK** | ✅ Zorunlu | <https://dotnet.microsoft.com/download/dotnet/8.0> → “Download .NET SDK x64” |
| Visual Studio 2022 Community | ⬜ Opsiyonel | XAML tasarımcısı + Hot Reload istersen (iş yükü: **.NET desktop development**) |
| Git for Windows | ✅ (kodu klonlarsan) | <https://git-scm.com> |
| Windows 10/11 x64 | ✅ | — |

Kontrol et:

```powershell
dotnet --version      # 8.0.x görmelisin
git --version
```

---

## 2. Kodu al

### A) Git **yoksa** → ZIP ile indir (en kolay, önerilen)

1. Şu adresi tarayıcıya yapıştır ve indir:
   ```
   https://github.com/kerem71099/GameShelf/archive/refs/heads/arena/01a107bc-gameshelf.zip
   ```
2. İnen ZIP'e **sağ tık → Tümünü ayıkla** (masaüstü iyidir).
   Klasör adı şuna benzer: `GameShelf-arena-01a107bc-gameshelf`
3. Klasörü aç; içinde `GameShelf.sln` ve `scripts` klasörünü görmelisin.
4. **PowerShell'i bu klasörde aç:** Explorer adres çubuğuna `powershell` yaz → Enter.
   (Veya klasörde boş bir yere **Shift + sağ tık → "PowerShell penceresini burada aç"**.)

### B) Git kurmak istersen (opsiyonel)

1. <https://git-scm.com/download/win> → indir → kur
   (kurulumda **“Git from the command line and also from 3rd-party software”** seçeneğini seç)
2. Kurulumdan sonra **yeni** bir PowerShell aç:

```powershell
git clone https://github.com/kerem71099/GameShelf.git
cd GameShelf
git checkout arena/01a107bc-gameshelf
```

> ZIP ile indirdiysen `git pull` çalışmaz (Git yok). Güncelleme için ya Git kur,
> ya ZIP'i yeniden indir.

## 3. Çalıştır — 3 farklı yol

### Yol 1: Tek tıkla `.bat` (en kolay)

Proje klasöründe `scripts` klasörünü aç → **`run.bat` dosyasına çift tıkla**.
(Çalıştırdığın yer `C:\WINDOWS\system32` ise `scripts\run.bat` yazmak işe yaramaz —
klasöre gidip çift tıklamak en güvenlisi.)
Sırayla `restore → build → run` yapar ve pencere açılır.
Hata olursa pencere kapanmaz, mesajı görebilirsin.

### Yol 2: Komut satırı

```powershell
cd C:\Users\<sen>\Projects\GameShelf
dotnet restore GameShelf.sln
dotnet build   GameShelf.sln -c Debug
dotnet run --project src\GameShelf.App\GameShelf.App.csproj
```

### Yol 3: Visual Studio 2022

1. `GameShelf.sln` dosyasına çift tıkla
2. Sağ üstte **başlangıç projesi** olarak `GameShelf.App` seçili olsun
3. **F5** (veya yeşil ▶ butonu)

İlk açılış 30–90 saniye sürebilir (NuGet paketleri iner, XAML derlenir).

---

## 4. İlk açılışta yapılacaklar (5 adım)

Uygulama açıldığında kütüphane boş görünür — normal. Sırayla:

1. **Ayarlar → Kütüphane klasörleri → “+ Oyun klasörü ekle”**
   Kendi oyun dosyalarının (`.cue/.bin/.chd/.iso`, PS3 oyun klasörleri) bulunduğu klasörü seç → **Kaydet**.
2. **Araçlar → Yeniden tara** (veya `F5`)
   Alt satırda kaç oyun eklendiğini görürsün.
3. **Ayarlar → Emülatörler**
   - PS1 → `duckstation-qt-x64-ReleaseLTCG.exe` yolunu ver (Gözat)
   - PS2 → `pcsx2-qtx64-avx2.exe`
   - PS3 → `rpcs3.exe`
   - İstersen **BIOS/firmware yolu** alanlarını doldur (yalnızca yol kaydedilir)
   → **Kaydet**
4. **Araçlar → BIOS / firmware kontrolü** → durumları gör
   (“yapılandırılmadı” bir uyarıdır, başlatmayı engellemez)
5. **Kütüphane → bir oyun seç → “Başlat”**
   Emülatörün açılır. Kapatınca süre kaydedilir.

> Emülatörleri **sen kurmuş olmalısın**; GameShelf emülatör, BIOS, oyun indirmez/dağıtmaz
> (bkz. `LEGAL.md`).

---

## 5. Veriler nerede?

```
%LOCALAPPDATA%\GameShelf\
├─ settings.json          ayarlar
├─ library.db             kütüphane (SQLite)
├─ Metadata\<id>\cover.png   kapak görselleri
├─ logs\app-2026-10-04.log   log dosyaları
└─ plugins\               (opsiyonel) plugin klasörleri
```

`scripts\open-data-folder.bat` ile tek tıkla açabilirsin.
Sıfırdan başlamak istersen: uygulamayı kapat → bu klasörü sil → yeniden aç.

---

## 6. Testleri çalıştır

```powershell
scripts\test.bat                    # hepsi
scripts\test.bat EmulatorLaunch     # sadece başlatma testleri
scripts\test.bat Scanner            # sadece tarama testleri
```

Beklenen: hepsi yeşil. (Testler gerçek emülatör **açmaz**, sahte process kullanır.)

---

## 7. Tek EXE olarak paketle

```powershell
scripts\publish-exe.bat
```

Soru sorar:

- **1** → framework-dependent (~8 MB, hedef PC’de .NET 8 Runtime gerekir)
- **2** → self-contained (~80 MB, hiçbir şey gerekmez)

Çıktı:

```
src\GameShelf.App\bin\Release\net8.0-windows\win-x64\publish\GameShelf.exe
```

**Mutlaka test et:** `GameShelf.exe` dosyasını başka bir klasöre kopyala ve oradan çalıştır.
Bu, SQLite native kütüphanesinin (`e_sqlite3.dll`) tek dosyadan düzgün açıldığını doğrular.

---

## 8. Sorun giderme

### 8.1 İlk derlemede en sık görülen 5 hata

| # | Hata | Anlamı | Çözüm |
|---|---|---|---|
| 1 | `MSB1003: Specify a project or solution file` | Yanlış klasördesin | `cd` ile `GameShelf.sln` olan klasöre git |
| 2 | `NETSDK1045: .NET SDK does not support targeting .NET 8.0` | SDK 8 yok (veya eski) | .NET **8** SDK kur → `dotnet --version` |
| 3 | `NETSDK1047: Assets file doesn't have a target for 'win-x64'` | RID paketi inmedi | `dotnet restore` → internet açık mı? Olmuyorsa `dotnet build GameShelf.sln` (RID'siz) dene |
| 4 | `error NU1101 / NU1102: Unable to find package ...` | NuGet erişimi yok | VPN/proxy kapat, `dotnet nuget locals all --clear`, `dotnet restore` |
| 5 | `error CS0246 / CS0535 / CS1061` | Kod derleme hatası | Aşağıdaki “hatayı paylaşma” adımlarıyla bana gönder |

> **Not (`NETSDK1047`)**: bu sürümde RID artık csproj’da sabit değil
> (`RuntimeIdentifiers`), yalnızca `publish-exe.bat` içinde `-r win-x64` geçiliyor.
> Depoyu en son hâline güncellediysen bu hata oluşmaz.

### 8.2 Hatayı bana nasıl göndereceksin (en hızlı yol)

```powershell
scripts\build-log.bat          # tum ciktiyii build-log.txt dosyasina yazar
```

`build-log.txt` dosyasını aç, içindeki **`error ...` ile başlayan ilk 10–15 satırı**
kopyalayıp bana yapıştır. (Alternatif: terminalde sağ tık → “Seç/hepsini seç” → kopyala.)

Veya ortam bilgini görmek için: `scripts\doctor.bat` → çıktısını paylaş.

### 8.3 Genel tablo

| Belirti | Çözüm |
|---|---|
| `git : The term 'git' is not recognized` | Git kurulu değil → ya ZIP ile indir (§2-A), ya Git kur (§2-B) |
| `cd : Cannot find path '...\GameShelf'` | Repo o klasörde değil → önce §2 ile kodu indir, sonra o klasöre `cd` yap |
| `MSB1009: Proje dosyası yok` / `MSBUILD : error MSB1009` | Yanlış klasördesin → `GameShelf.sln` olan klasöre git |
| `scripts\build-log.bat : The module 'scripts' could not be loaded` | PowerShell `.bat` dosyalarını böyle çalıştırmaz → proje klasöründe `scripts\run.bat` **çift tıkla**, ya da `cmd` / `powershell -File .\scripts\build-log.bat` kullan |
| `'dotnet' is not recognized` | .NET 8 SDK kurulu değil → yükle, **yeni** bir PowerShell aç |
| `NETSDK1100: Windows is required` | WPF yalnızca Windows’ta derlenir (Linux/WSL değil) |
| `DllNotFoundException: e_sqlite3` | `publish` sonrası EXE’yi başka klasörde denedin mi? `IncludeNativeLibrariesForSelfExtract` açık olmalı (csproj’da açık) |
| `error NU1101: Unable to find package` | İnternet/NuGet erişimi yok → `dotnet restore` tekrar dene |
| Derleme hatası: `Brush.Xxx` bulunamadı | `Themes/Theme.Dark.xaml` ve `Theme.Light.xaml` aynı anahtarları içeriyor mu? |
| Uygulama açılışta kapanıyor | `%LOCALAPPDATA%\GameShelf\logs\startup-error.txt` dosyasına bak |
| Tarama hiç oyun bulmuyor | Ayarlarda klasör eklendi mi + `IsEnabled` açık mı + uzantı destekleniyor mu (`.cue/.bin/.iso/.chd/.pbp`, PS3 klasör) |
| Emülatör açılıyor ama oyun yüklenmiyor | `Araçlar → Launch log` satırındaki komutu kopyala, `cmd`’de elle çalıştır; gerekirse Ayarlar’daki **argüman şablonunu** emülatörünün `--help` çıktısına göre düzelt |
| PowerShell betik çalıştırma uyarısı | `.bat` dosyaları PowerShell değil; çift tıkla veya `cmd`’den çalıştır |

---

## 9. Güncelleme

Depodaki yeni sürümü almak için:

```powershell
cd C:\Users\<sen>\Projects\GameShelf
git pull
scripts\run.bat
```

---

## 10. Özet komut seti (kopyala-yapıştır)

```powershell
git clone https://github.com/kerem71099/GameShelf.git
cd GameShelf
git checkout arena/01a107bc-gameshelf
dotnet restore GameShelf.sln
dotnet build   GameShelf.sln -c Debug
dotnet run --project src\GameShelf.App\GameShelf.App.csproj
```

Sonra uygulamada: **Ayarlar → klasör ekle → Kaydet → Araçlar → Yeniden tara → emülatör yolu → Başlat**.
