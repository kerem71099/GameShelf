# 07 — Adım Adım Uygulama Rehberi

Bu rehber, GameShelf'i **sıfırdan** kurmak isteyen biri için yazılmıştır. Bu depoda zaten
çalışan bir iskelet var; adımlar "hangi dosyayı, hangi sırada, neyi doğrulayarak yazacaksın"
mantığıyla ilerler. Her adımın sonunda **✔ Doğrulama** ve **🛑 Sık hata** kutuları var.

> Komutlar **PowerShell** içindir (Windows 10/11). Linux/macOS'ta WPF derlenmez.

---

## Adım 0 — Ön hazırlık (30 dk)

1. **.NET 8 SDK** kur: <https://dotnet.microsoft.com/download> → `dotnet --version` → `8.0.x`
2. **Visual Studio 2022** (Community yeterli) → iş yükü: **“.NET desktop development”**
   (WPF designer + Hot Reload için). Alternatif: VS Code + `dotnet` CLI (XAML önizlemesi olmaz).
3. **DB Browser for SQLite** (opsiyonel ama çok faydalı): <https://sqlitebrowser.org>
   → veritabanını gözle görmek için.
4. Depoyu klonla, branch'i kontrol et:
   ```powershell
   git clone https://github.com/<sen>/GameShelf.git
   cd GameShelf
   git checkout arena/01a107bc-gameshelf
   ```
5. NuGet geri yükleme + ilk derleme:
   ```powershell
   dotnet restore GameShelf.sln
   dotnet build   GameShelf.sln -c Debug
   ```
   **Beklenen:** 5 proje de başarılı. `GameShelf.App` yalnızca Windows'ta derlenir
   (`net8.0-windows` + `UseWPF`).

✔ **Doğrulama:** `dotnet test tests/GameShelf.Tests/GameShelf.Tests.csproj` → testler yeşil.
🛑 **Sık hata:** `NETSDK1100: Windows is required` → Linux/macOS'ta WPF projesi derlenemez.

---

## Adım 1 — Solution ve proje iskeleti (1 saat)

```powershell
dotnet new sln -n GameShelf
dotnet new classlib -n GameShelf.Domain        -o src/GameShelf.Domain        -f net8.0
dotnet new classlib -n GameShelf.Application   -o src/GameShelf.Application   -f net8.0
dotnet new classlib -n GameShelf.Infrastructure -o src/GameShelf.Infrastructure -f net8.0
dotnet new wpf      -n GameShelf.App           -o src/GameShelf.App
dotnet new xunit    -n GameShelf.Tests         -o tests/GameShelf.Tests

dotnet sln add src/GameShelf.Domain src/GameShelf.Application `
              src/GameShelf.Infrastructure src/GameShelf.App tests/GameShelf.Tests
```

Bağımlılıklar (ProjectReference):

```
App → Application, Infrastructure
Infrastructure → Application, Domain
Application → Domain
Tests → Application, Domain, Infrastructure
```

Ortak ayarlar için köke `Directory.Build.props` ekle (`LangVersion=12`, `Nullable=enable`,
`ImplicitUsings=enable`).

NuGet paketleri:

```powershell
dotnet add src/GameShelf.Application      package CommunityToolkit.Mvvm
dotnet add src/GameShelf.Application      package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add src/GameShelf.Infrastructure   package Microsoft.Data.Sqlite
dotnet add src/GameShelf.Infrastructure   package Dapper
dotnet add src/GameShelf.Infrastructure   package Microsoft.Extensions.DependencyInjection
dotnet add src/GameShelf.App              package Microsoft.Extensions.DependencyInjection
```

✔ **Doğrulama:** Boş WPF penceresi açılıyor (`dotnet run --project src/GameShelf.App`).

---

## Adım 2 — Domain katmanı (2 saat)

Sırayla yaz (hiçbir dış bağımlılık yok):

| # | Dosya | İçerik |
|---|---|---|
| 1 | `Enums/PlatformId.cs` | `Unknown=0, Ps1..Ps5` — **değerler DB ile birebir aynı kalmalı** |
| 2 | `Enums/LaunchIssueSeverity.cs` | Info / Warning / Error |
| 3 | `Entities/Game.cs` | Id, Title, SortTitle, PlatformId, FilePath, ContentHash, CoverPath, Notes, IsFavorite, IsMissing, AddedAt, LastPlayedAt, PlayTimeMinutes, LibraryFolderId |
| 4 | `Entities/Platform.cs` | + `Extensions` (DefaultExtensions → liste) |
| 5 | `Entities/EmulatorConfig.cs` | exe yolu, argüman şablonu, tam ekran, ek argümanlar |
| 6 | `Entities/GameOverride.cs` | oyun bazlı ezme (Fullscreen `bool?` = varsayılan) |
| 7 | `Entities/LibraryFolder.cs`, `Entities/LaunchHistoryEntry.cs` | |
| 8 | `Models/LaunchIssue.cs`, `Models/EmulatorLaunchContext.cs` | değer nesneleri |
| 9 | `Interfaces/IEmulatorBackend.cs` | **en önemli sözleşme** (argüman üret + doğrula) |
| 10 | `Extensions/PlatformIdExtensions.cs` | `ToShortName()`, `IsLaunchable()` |

**Kural:** Domain'de `System.IO`, `System.Data`, WPF **yok**. Entity'ler POCO.

✔ **Doğrulama:** Proje tek başına derleniyor ve hiçbir projeye referans vermiyor.

---

## Adım 3 — Application: arayüzler + modeller (2 saat)

1. `Abstractions/`: `ILoggingService`, `ILibraryRepository`, `ISettingsService`,
   `IGameScannerService`, `IEmulatorLaunchService`, `IProcessLauncher` (+`ILaunchedProcess`),
   `IFileHasher`, `IPlatformDetector`, `IEmulatorBackendFactory`, `ICoverImageService`,
   `IDialogService`, `IDispatcher`, `IShellService`, `IPluginLoader`.
   > `IProcessLauncher` ve `IDialogService`, test edilebilirlik için var: emülatör/pencere
   > açmadan test yazabiliyoruz.
2. `Models/`: `AppSettings` (+ `LibraryViewMode`, `GameSort`, `WindowSettings`),
   `LibraryFilter`, `ScanResult`/`ScanProgress`/`PlatformDetection`,
   `LaunchValidation`/`LaunchOutcome`, `BiosStatus`, `LogLevel`.

**Kural:** Application, Infrastructure'ı **bilmez**; sadece kendi arayüzlerini kullanır.

---

## Adım 4 — Infrastructure: veritabanı (3 saat)

1. `Data/Schema.cs` → DDL'yi `string[]` olarak yaz (her ifade ayrı eleman; `docs/03` ile aynı).
2. `Data/SqliteConnectionFactory.cs` → WAL + `ForeignKeys=true`; kurucuda `PRAGMA` çalıştır.
3. `Data/DbInitializer.cs` → şemayı uygula + `Platforms` ve `EmulatorConfigs` tohumla.
4. `Repositories/RowMappers.cs` → **elle eşleme**: satır sınıfları yalnızca `string`/`long`,
   dönüşüm `ToGame()` vb. (Dapper'ın tarih/Guid dönüşümüne güvenmiyoruz → sürümden sürüme
   farklılık olmaz).
5. `Repositories/SqliteLibraryRepository.cs` → Dapper ile CRUD.

Sıralama ipucu: önce `GetGamesAsync` + `UpsertGameAsync` yaz, **hemen test et**:

```powershell
dotnet test --filter "FullyQualifiedName~SqliteLibraryRepositoryTests"
```

🛑 **Sık hata:** `SELECT *` + Dapper eşlemesinde kolon adı ile property adı birebir aynı olmalı.
🛑 **Sık hata:** `LIMIT @Take OFFSET @Skip` için `filter.Skip/Take` mutlaka parametre olarak geçmeli.

---

## Adım 5 — Infrastructure: ayarlar + log (1 saat)

1. `OS/AppPaths.cs` → tüm yollar tek yerde; `EnsureDefaults()` klasörleri oluşturur.
2. `Settings/JsonSettingsService.cs` → atomic yazma (`*.tmp` → `File.Move(overwrite:true)`),
   bozuk JSON'da varsayılanlara dön.
3. `Logging/FileLoggingService.cs` → `logs/app-yyyyMMdd.log`, `lock` ile thread-safe, 7 gün saklar.

✔ **Doğrulama:** `JsonSettingsServiceTests` yeşil.

---

## Adım 6 — Infrastructure: platform tespiti + tarama (4 saat)

1. `Platform/PlatformCatalog.cs` → tohum platform listesi (PS4/PS5 `IsSupported=false`).
2. `Platform/PlatformDetector.cs` → sıra:
   **dizin yapısı → magic byte → uzantı → klasör ipucu → Unknown**.
   Emin değilse **asla** zorla platform atama.
3. `OS/PartialFileHasher.cs` → `boyut:ilk512KB:son512KB` (GB'lık ISO'lar için).
4. `Application/Services/GameScannerService.cs` → aday topla → tespit → hash → duplicate → upsert
   → sonunda kayıp dosyaları işaretle.
5. `Application/Services/LibraryMaintenanceService.cs` → Tools ekranı için arama/yeniden tarama.

**Test sırası:**
```powershell
dotnet test --filter "FullyQualifiedName~PlatformDetectorTests"
dotnet test --filter "FullyQualifiedName~GameScannerServiceTests"
```

🛑 **Sık hata:** Aynı klasör iki kez taranınca `Added` tekrar artıyorsa upsert SQL'ini kontrol et
(`ON CONFLICT(FilePath)`).
🛑 **Sık hata:** Duplicate tespiti çalışmıyorsa hash `null` dönüyordur (dosya kilidi/erişim hatası).

---

## Adım 7 — Infrastructure: emülatör adapter'ları (3 saat)

1. `Emulators/EmulatorArgumentRenderer.cs` → `{game} {fullscreen} {extra} {bios}` token'ları
   + otomatik tırnaklama + fazla boşluk temizliği.
2. `Emulators/EmulatorBackendBase.cs` → ortak doğrulama (uzantı uyarısı, BIOS uyarısı) +
   şablon seçimi (override → config → varsayılan).
3. `Emulators/PlayStationBackends.cs` → `DuckStationBackend`, `Pcsx2Backend`, `Rpcs3Backend`
   (yalnızca resmî CLI bayrakları; `docs/04`).
4. `Emulators/EmulatorBackendFactory.cs` → platform → backend; plugin'leri de buraya kaydet.
5. `OS/ProcessLauncher.cs` → `Process.Start` sarmalayıcı (`UseShellExecute=false`).

```powershell
dotnet test --filter "FullyQualifiedName~EmulatorBackendTests"
dotnet test --filter "FullyQualifiedName~EmulatorLaunchServiceTests"
```

**Gerçek emülatörle ilk deneme (manuel):**
- DuckStation'ı kur, `Settings → Emülatörler → PS1 → Gözat` ile exe'yi seç.
- Bir `.cue` dosyası ekle, `Başlat`.
- `Tools → Launch log` sekmesinde üretilen komut satırını kopyala, `cmd` içinde kendin çalıştırıp
  karşılaştır. Fark varsa **argüman şablonunu** düzelt (emülatör sürümleri bayrak değiştirebilir).

---

## Adım 8 — Application: servisler (2 saat)

1. `Services/EmulatorLaunchService.cs` → doğrulama → argüman → başlat → history +
   `LastPlayedAt` → arka planda `WaitForExitAsync` ile oynama süresi.
2. `Services/CoverImageService.cs` → kapağı `Metadata/<guid>/cover.<ext>` altına kopyala
   (PNG/JPEG imza kontrolü; **internetten indirme yok**).
3. `Services/BiosCheckService.cs` → yalnızca yol varlık kontrolü + yasal not.
4. `Services/TitleCleaner.cs` → dosya adından okunabilir başlık + `SortTitle` + baş harfler.
5. `DependencyInjection.cs` → `AddApplication()`.

🛑 **Sık hata:** `TrackAsync` (süre takibi) `async void` olmamalı; `_ = TrackAsync(...)` ile
başlat ve içinde `try/catch` tut.

---

## Adım 9 — Application: ViewModel'ler (4 saat)

Sıra: `ViewModelBase` → `GameItemViewModel` → `LibraryViewModel` → `GameDetailsViewModel` →
`SettingsViewModel` → `ToolsViewModel` → `MainViewModel`.

Kurallar:
- `[ObservableProperty]` + `[RelayCommand]`/`[AsyncRelayCommand]` (CommunityToolkit).
- `ObservableCollection` **yalnızca** `IDispatcher.InvokeAsync` içinde güncellenir.
- ViewModel'de `MessageBox`/`OpenFileDialog` **yok** → `IDialogService`.
- Uzun işler: `RunBusyAsync(...)`, `IsBusy`, `StatusMessage`, `CancellationTokenSource`.

`MainViewModel` bağlantıları:
```
Library.DetailsRequested  → ShowGameDetails(game) → CurrentView = Details
Library.SettingsRequested → ShowSettings()
Details.BackRequested     → ShowLibrary()
SelectedNav değişimi      → Navigate(item)   (null ise dokunma: detay ekranı)
ThemeChanged              → App katmanında ThemeService.Apply(theme)
FocusSearchRequested      → MainWindow arama kutusuna odaklanır
```

---

## Adım 10 — App: tema ve stiller (3 saat)

1. `Themes/Tokens.xaml` → tema bağımsız ölçekler (4 px grid, köşe yarıçapları, yazı boyutları).
2. `Themes/Theme.Dark.xaml` + `Themes/Theme.Light.xaml` → **aynı anahtarlarla** fırçalar.
3. `Themes/Styles.xaml` → bileşen stilleri; renkler **mutlaka** `{DynamicResource ...}`.
4. `Services/ThemeService.cs` → çalışma zamanında sözlüğü değiştir.

> XAML'de `<!-- ... -->` yorumlarının içinde `--` **OLAMAZ** (XML kuralı). Ayırıcı çizgi yerine
> `=` kullan.

🛑 **Sık hata:** `StaticResource Brush.Xxx` bulunamadı → iki temada da aynı anahtar var mı?
🛑 **Sık hata:** Tema değişince renkler güncellenmiyor → `DynamicResource` kullan.

---

## Adım 11 — App: ekranlar (6 saat)

Sıra: `MainWindow` → `LibraryView` → `GameDetailsView` → `SettingsView` → `ToolsView`.

Her ekran için kontrol listesi:
- [ ] `DataContext` XAML'de **değil**, `MainWindow` üzerinden geliyor (ContentControl + DataTemplate).
- [ ] Tüm metinler `Style="{StaticResource Text...}"`.
- [ ] Ölçekler `{StaticResource Space.X}"`, renkler `{DynamicResource Brush...}`.
- [ ] Boş durum (empty state) var mı?
- [ ] Kısayollar (`Window.InputBindings` / `UserControl.InputBindings`) bağlandı mı?
- [ ] `AutomationProperties.Name` ikon butonlarında var mı?

Kısayol tablosu: `docs/02-UI-UX-Tasarim-Dili.md` §6.

---

## Adım 12 — Composition root ve hata yönetimi (2 saat)

`App.xaml.cs` sırası:

```
1) FileLoggingService (henüz DI yok: yol gerekiyor)
2) JsonSettingsService → LoadAsync() → AppPaths.EnsureDefaults()
3) ServiceCollection → AddGameShelfInfrastructure(settings, path) + AddApplication()
   + IDispatcher + IDialogService + ThemeService
4) BuildServiceProvider → App.Services
5) DbInitializer.InitializeAsync()
6) EmulatorBackendFactory.LoadPlugins()
7) ThemeService.Apply(settings.Theme)
8) MainWindow → Show() → MainViewModel.InitializeAsync()
```

Exception handling (`SetupExceptionHandling()`):
- `DispatcherUnhandledException` → log + dialog + `args.Handled = true`
- `AppDomain.CurrentDomain.UnhandledException` → log
- `TaskScheduler.UnobservedTaskException` → `SetObserved()` + log
- `OnStartup` içinde try/catch → `startup-error.txt` + MessageBox + `Shutdown(1)`

---

## Adım 13 — İlk çalıştırma ve manuel smoke test (1 saat)

```powershell
dotnet run --project src/GameShelf.App/GameShelf.App.csproj
```

1. `%LOCALAPPDATA%\GameShelf\` klasörü oluştu mu? (`settings.json`, `library.db`, `logs\`, `Metadata\`)
2. `Ayarlar → Kütüphane klasörleri → + Oyun klasörü ekle` → kendi oyun klasörünü ver → Kaydet.
3. `Araçlar → Yeniden tara` → sayılar doğru mu?
4. `Ayarlar → Emülatörler` → her platform için exe yolu ver → Kaydet.
5. `Araçlar → BIOS / firmware kontrolü` → durum satırları (yapılandırılmadı / bulundu).
6. Kütüphaneden bir oyun seç → `Başlat` → emülatör açıldı mı?
7. Oyunu kapat → `LastPlayedAt` ve süre güncellendi mi?
8. `Ctrl+T` ile tema değiştir → tüm ekranlar okunabilir mi?
9. Ağı kapat (uçak modu) → uygulama hâlâ sorunsuz çalışıyor mu? **Olmalı.**

---

## Adım 14 — Testler (3 saat)

```powershell
dotnet test tests/GameShelf.Tests/GameShelf.Tests.csproj
dotnet test --filter "FullyQualifiedName~EmulatorLaunchServiceTests"   # smoke test
```

Yazılacak testler (liste: `docs/06-Yol-Haritasi-ve-Test-Plani.md` §4.2):
1. `PlatformDetectorTests` — 6 senaryo (cue / PS3 klasör / chd+ipucu / iso marker / pbp / bilinmeyen)
2. `GameScannerServiceTests` — ekleme, tekrar tarama, duplicate, kayıp dosya, ilerleme, inceleme
3. `EmulatorBackendTests` — argüman üretimi (3 emülatör + özel şablon + otomatik tırnak)
4. `EmulatorLaunchServiceTests` — **launch smoke test** (sahte `IProcessLauncher`)
5. `SqliteLibraryRepositoryTests` — gerçek SQLite (geçici dosya)
6. `JsonSettingsServiceTests` — varsayılan / round-trip / bozuk dosya

🛑 **Sık hata:** Test hiçbir zaman gerçek emülatör açmamalı → `FakeProcessLauncher` şart.
🛑 **Sık hata:** ObservableCollection'ı testte UI thread'i olmadan güncelleme → `ImmediateDispatcher`.

---

## Adım 15 — Dağıtım: tek EXE (1 saat)

```powershell
# Framework-dependent (~5-8 MB)
dotnet publish src/GameShelf.App/GameShelf.App.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true

# Self-contained (~45-80 MB)
dotnet publish src/GameShelf.App/GameShelf.App.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true
```

Çıktı: `src/GameShelf.App\bin\Release\net8.0-windows\win-x64\publish\GameShelf.exe`

**Kritik kontrol:** EXE'yi temiz bir klasöre kopyala ve çalıştır. SQLite native
(`e_sqlite3.dll`) açılamazsa `IncludeNativeLibrariesForSelfExtract=true` eksiktir.

Opsiyonel installer (v1): Inno Setup betiği → `publish\` içeriğini
`%LOCALAPPDATA%\Programs\GameShelf` altına kopyalar + Start Menu kısayolu.
MSIX istersen: VS → "Windows Application Packaging Project" (sideload sertifikası gerekir).

---

## Adım 16 — Plugin denemesi (opsiyonel, 1 saat)

1. `%LOCALAPPDATA%\GameShelf\plugins\ornek-ps4\plugin.json` oluştur (`docs/05` şeması).
2. Zorunlu alanlar: `declaresNoDrmBypass`, `declaresNoRomDistribution`,
   `declaresOfflineOnly` → **true**; `capabilities.network` → **false**.
3. `codeFree: true` ile başla (DLL yüklemeden çalışır).
4. Uygulamayı yeniden başlat → log'da `Plugin yüklendi (kodsuz)` satırını gör.
5. DLL moduna geçmek istersen: derle, SHA-256 al, manifest'e yaz; aksi hâlde yüklenmez.

---

## Hata ayıklama rehberi (ilk 10 problem)

| Belirti | Olası sebep | Çözüm |
|---|---|---|
| `DllNotFoundException: e_sqlite3` | single-file native ayarı | `IncludeNativeLibrariesForSelfExtract=true` |
| Tema değişmiyor | `StaticResource` kullanılmış | Fırçalarda `DynamicResource` |
| Tarama hiç oyun bulmuyor | uzantı listesi / klasör `IsEnabled=false` | Ayarlardaki klasörü ve `PlatformCatalog.DefaultExtensions` kontrol et |
| Emülatör açılıyor ama oyun yüklenmiyor | argüman şablonu | `Tools → Launch log` komutunu kopyala, elle dene, şablonu düzelt |
| Emülatör açılmıyor (Win32) | exe yolu yanlış / çalışma dizini | `WorkingDirectory` exe klasörü olsun |
| Kapak görünmüyor | dosya taşınmış / relative path | `Metadata\<id>\cover.png` var mı, DB'deki yol güncel mi? |
| Filtre çalışmıyor | `SelectedPlatform` null | `LoadPlatformFiltersAsync` “Tümü” öğesini başta ekliyor mu? |
| Arama yavaş | her tuşta DB sorgusu | 250 ms debounce var mı (`OnSearchTextChanged`)? |
| Oyun süresi artmıyor | `TrackAsync` hata verdi | log'da "Oyun süresi kaydedilemedi" ara |
| Uygulama açılışta kapanıyor | `OnStartup` exception | `%LOCALAPPDATA%\GameShelf\logs\startup-error.txt` |

---

## MVP → v1 kontrol listesi

**MVP (tamamlandığında ürün çalışır)**
- [ ] 4 katman + DI + tek EXE
- [ ] Şema + repository (upsert doğru)
- [ ] Klasör ekleme + recursive tarama + platform tahmini + duplicate
- [ ] Library grid + arama + platform/favori filtresi
- [ ] Emülatör yolları (PS1/PS2/PS3) + argüman şablonu
- [ ] Başlatma + doğrulama + launch log
- [ ] Global exception handling + dosya log'u

**v1**
- [ ] Game Details (kapak, not, region, yol, override)
- [ ] Liste görünümü + sıralama + son oynananlar
- [ ] Dark/Light tema kalıcılığı + tüm kısayollar
- [ ] Tools (BIOS kontrolü, kayıp dosyalar, içe aktarılmamış dosyalar, launch log ekranı)
- [ ] Oynama süresi takibi
- [ ] Plugin loader (manifest-only) + PS4/PS5 placeholder
- [ ] Portable mod + yedekleme (`VACUUM INTO`)

---

## Günlük çalışma ritmi (öneri)

1. **Sabah:** `git pull` → `dotnet build` → `dotnet test` (yeşil olduğundan emin ol).
2. **Öğleden önce:** tek bir katmana odaklan (ör. sadece Infrastructure).
3. **Her özellik sonrası:** ilgili testi yaz → `dotnet test --filter ...`
4. **Akşam:** `dotnet run` ile manuel smoke test (Adım 13'ün 9 maddesi).
5. **Commit:** küçük ve açıklayıcı (`feat(scanner): duplicate detection via partial hash`).
6. **Haftada bir:** `dotnet publish` + temiz klasörde EXE testi.

İyi çalışmalar — sorun çıkarsa ilk bakılacak yer her zaman
`%LOCALAPPDATA%\GameShelf\logs\app-<tarih>.log`.
