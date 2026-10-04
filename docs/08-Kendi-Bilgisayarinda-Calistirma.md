# 08 — Kendi Bilgisayarında Çalıştırma (Windows 10/11)

Bu dosya, GameShelf'i kendi PC'nde **ilk kez** ayağa kaldırmak için gereken adımları anlatır.
Tahmini süre: **15–20 dakika** (indirmeler hariç).

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

### A) Git ile (önerilen)

```powershell
git clone https://github.com/kerem71099/GameShelf.git
cd GameShelf
git checkout arena/01a107bc-gameshelf
```

### B) Git kullanmadan

1. GitHub’da branch `arena/01a107bc-gameshelf` → **Code → Download ZIP**
2. ZIP’i bir klasöre aç (örn. `C:\Users\<sen>\Projects\GameShelf`)
3. O klasörde PowerShell’i aç

> Kısa yol: `scripts\` klasöründeki `.bat` dosyalarına çift tıklayabilirsin.

---

## 3. Çalıştır — 3 farklı yol

### Yol 1: Tek tıkla `.bat` (en kolay)

`scripts\run.bat` dosyasına **çift tıkla**.
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

| Belirti | Çözüm |
|---|---|
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
