# 00 — Teknoloji Seçimi ve Proje Yapısı

## 1. Karar tablosu (özet)

| Konu | Seçim | Alternatif | Neden bu? |
|---|---|---|---|
| Platform | **Windows 10/11** | — | Hedef zaten sadece Windows |
| UI framework | **.NET 8 (LTS) + WPF** | WinUI 3 | Aşağıda ↓ |
| Desen | **MVVM** (CommunityToolkit.Mvvm) | MVU / code-behind | Data binding + test edilebilir VM |
| DI | **Microsoft.Extensions.DependencyInjection** | Autofac / el yazımı | `ServiceCollection` 30 satır |
| Veritabanı | **SQLite (Microsoft.Data.Sqlite) + Dapper** | LiteDB / EF Core | Aşağıda ↓ |
| Ayarlar | **System.Text.Json → settings.json** | Registry / .ini | Taşınabilir, yedeklenebilir, atomic write |
| Log | **Kendi `FileLoggingService`** | Serilog | Offline, bağımlılık yok; istersek sonra Serilog'a taşınır |
| Test | **xUnit** (+ kendi fake'leri) | NUnit / MSTest | `dotnet test` ile sorunsuz |
| Dağıtım | **PublishSingleFile** (+ opsiyonel Inno Setup / MSIX) | ClickOnce | Tek EXE isteği |

## 2. Neden WPF? (WinUI 3 değil)

**En kolay seçenek WPF**, çünkü:

1. **Runtime bağımlılığı yoktur.** WinUI 3 (Windows App SDK) projeleri hedef makinede Windows App SDK
   runtime'ının kurulu/uyumlu sürümünü ister (bootstrapper + sürüm matrisi). WPF doğrudan
   `Microsoft.WindowsDesktop.App` ile gelir; .NET 8 Desktop Runtime yeterlidir.
2. **Tek EXE publish WPF'de olgun.** WinUI 3'te self-contained/single-file ve unpackaged senaryoları
   daha fazla dikkat ister (WindowsAppSDK bootstrap, `WindowsAppSDKSelfContained`).
3. **XAML + DataTemplate + Trigger + ResourceDictionary** ikisinde de var; WPF'in ekosistemi
   (ör. `Microsoft.Xaml.Behaviors.Wpf`) daha olgun ve örnek bolluğu var.
4. **Tooling:** VS 2022 WPF designer + XAML Hot Reload hazır çalışır.
5. Hedefimiz **offline tek kullanıcı masaüstü uygulaması**; Fluent/WinUI'nin getirdiği ekstra
   modern kontrol seti bu uygulama için maliyete değmez (grid/list + detay + ayarlar ekranı).

> WinUI 3'e **taşınabilirlik notu:** ViewModel'ler framework'ten bağımsız (sadece
> `CommunityToolkit.Mvvm` kullanıyor). Tekrar yazılacak olan yalnızca `GameShelf.App` katmanıdır.

## 3. Neden SQLite + Dapper? (LiteDB / EF Core değil)

- **LiteDB**: tek dosya, şemasız, gerçekten kolay — ama: sürüm yükseltmelerinde dosya formatı
  riski, LINQ sorgularının SQL'e göre daha az şeffaf olması, "veritabanını dışarıdan inceleme"
  imkânının zayıf olması (DB Browser for SQLite gibi bir araç yok).
- **EF Core**: güçlü ama migration/ DbContext yaşam döngüsü/tracking, offline tek kullanıcı
  uygulaması için gereksiz ağırlık.
- **SQLite + Dapper (seçim):** Şema SQL ile okunabilir, `DB Browser for SQLite` ile kullanıcı
  verisini görebiliriz, `INSERT ... ON CONFLICT` ile upsert tek satır, Dapper map işini
  otomatik yapar. ~200 satır SQL ile tüm erişim katmanı biter.

> Not: `Microsoft.Data.Sqlite` (ADO.NET) + `Dapper` kullanıyoruz. EF Core provider'ı
> (`Microsoft.EntityFrameworkCore.Sqlite`) eklemiyoruz.

## 4. Projeler ve sorumluluklar

```
src/GameShelf.Domain/          netstandard'vari saf katman (net8.0)
├─ Entities/                   Game, Platform, EmulatorConfig, GameOverride, LibraryFolder, LaunchHistoryEntry
├─ Enums/                      PlatformId, LaunchIssueSeverity, ...
├─ Models/                     EmulatorLaunchContext, LaunchIssue (değer nesneleri)
└─ Interfaces/                 IEmulatorBackend

src/GameShelf.Application/     use-case katmanı (net8.0)
├─ Abstractions/               ILibraryRepository, ISettingsService, IGameScannerService,
│                              IEmulatorLaunchService, ILoggingService, IProcessLauncher,
│                              IFileHasher, IPlatformDetector, IEmulatorBackendFactory,
│                              ICoverImageService, IDialogService, IPluginLoader
├─ Models/                     AppSettings, ScanResult, LibraryFilter, LaunchOutcome, ...
├─ Services/                   GameScannerService, EmulatorLaunchService, LibraryMaintenanceService,
│                              BiosCheckService, CoverImageService
└─ ViewModels/                 MainViewModel, LibraryViewModel, GameDetailsViewModel,
                               SettingsViewModel, ToolsViewModel, GameItemViewModel

src/GameShelf.Infrastructure/  dış dünya (net8.0)
├─ Data/                       Schema (DDL), SqliteConnectionFactory, DbInitializer
├─ Repositories/               SqliteLibraryRepository (Dapper)
├─ Settings/                   JsonSettingsService
├─ Logging/                    FileLoggingService
├─ Emulators/                  EmulatorBackendBase, DuckStationBackend, Pcsx2Backend,
│                              Rpcs3Backend, EmulatorBackendFactory
├─ Platform/                   PlatformDetector, PlatformCatalog (tohum veri)
├─ Plugins/                    PluginManifest, PluginLoader
├─ System/                     ProcessLauncher, PartialFileHasher, AppPaths
└─ DependencyInjection.cs      AddInfrastructure()

src/GameShelf.App/             WPF (net8.0-windows, UseWPF)
├─ App.xaml(.cs)               composition root + global exception handling
├─ Views/                      MainWindow, LibraryView, GameDetailsView, SettingsView, ToolsView
├─ Converters/
├─ Themes/                     Tokens.xaml, Theme.Dark.xaml, Theme.Light.xaml, Styles.xaml
└─ Services/                   DialogService (OpenFileDialog/FolderBrowserDialog), ThemeService

tests/GameShelf.Tests/         xUnit
```

**Bağımlılık yönü:** `App → Application → Domain ← Infrastructure`.
Infrastructure, Application'daki arayüzleri uygular; Application, Domain'i bilir;
Domain hiçbir şeyi bilmez.

## 5. NuGet paketleri

| Proje | Paket | Sürüm* | Amaç |
|---|---|---|---|
| Application | `CommunityToolkit.Mvvm` | 8.x | `ObservableObject`, `[ObservableProperty]`, `RelayCommand`, `WeakReferenceMessenger` |
| Application | `Microsoft.Extensions.DependencyInjection.Abstractions` | 8.x | arayüzler |
| Infrastructure | `Microsoft.Data.Sqlite` | 8.x | SQLite ADO.NET provider (native `e_sqlite3` dahil) |
| Infrastructure | `Dapper` | 2.x | SQL → nesne eşleme |
| Infrastructure | `Microsoft.Extensions.DependencyInjection` | 8.x | DI container |
| Infrastructure | `System.Text.Json` | (SDK içinde) | ayarlar |
| App | `Microsoft.Extensions.DependencyInjection` | 8.x | composition root |
| App | `Microsoft.Xaml.Behaviors.Wpf` | 1.x | (opsiyonel) `EventToCommand`, `InvokeCommandAction` |
| Tests | `xunit` + `xunit.runner.visualstudio` | 2.x | test koşucu |
| Tests | `Microsoft.NET.Test.Sdk` | 17.x | test host |
| Tests | `NSubstitute` | 5.x | (opsiyonel) mock: `IProcessLauncher` |

\* Sürümleri en son stable olarak güncelle: `dotnet list package --outdated`.

> **Test notu:** Dosya sistemi için `System.IO.Abstractions` **kullanmıyoruz**; scanner testlerinde
> gerçek temp klasör kullanıyoruz (daha az bağımlılık, daha gerçekçi). Sadece process başlatma
> (`IProcessLauncher`) mocklanır.

## 6. Publish (tek EXE)

`src/GameShelf.App/GameShelf.App.csproj` içinde:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>false</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  <DebugType>embedded</DebugType>
  <ApplicationIcon>Assets\GameShelf.ico</ApplicationIcon>
</PropertyGroup>
```

Komutlar:

```powershell
# framework-dependent (~5-8 MB) — hedefte .NET 8 Desktop Runtime gerekir
dotnet publish src/GameShelf.App/GameShelf.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# self-contained (~80 MB, sıkıştırılmış ~45 MB) — runtime gerekmez
dotnet publish src/GameShelf.App/GameShelf.App.csproj -c Release -r win-x64 --self-contained true  -p:PublishSingleFile=true
```

**Dikkat (SQLite + single file):** `IncludeNativeLibrariesForSelfExtract=true` olmadan
`e_sqlite3.dll` tek dosya paketinden yüklenemez ve ilk DB erişiminde `DllNotFoundException` alırsın.

### Opsiyonel installer

1. **Inno Setup (en kolay):** `GameShelf.iss` → `publish\` klasöründeki tek EXE'yi
   `%LOCALAPPDATA%\Programs\GameShelf` altına kopyalar, Start Menu kısayolu + kaldırıcı üretir.
2. **MSIX:** VS'ye "Windows Application Packaging Project" ekle → `Package.appxmanifest`.
   Mağaza dışı yükleme için sertifika gerekir; kullanıcıya "sideload" yükü bindirir.
   MVP için **gerekmez**, v1'e bırak.

## 7. Veri/klasör yerleşimi (runtime)

```
%LOCALAPPDATA%\GameShelf\
├─ settings.json            (AppSettings)
├─ library.db               (SQLite; taşınabilir modda exe'nin yanında da olabilir)
├─ Metadata\<gameId>\cover.png
├─ logs\app-2026-10-04.log
└─ plugins\<pluginId>\plugin.json (+ opsiyonel dll)
```

Yollar `Infrastructure/System/AppPaths.cs` içinde tek yerde üretilir.
Portable mod (v1 opsiyonu): exe'nin yanında `portable.txt` varsa tüm yollar exe dizinine döner.
