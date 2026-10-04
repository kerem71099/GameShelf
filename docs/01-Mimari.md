# 01 — Mimari

## 1. Katmanlar ve bağımlılıklar

```
┌──────────────────────────────────────────────────────────────┐
│ UI  (GameShelf.App, WPF)                                     │
│   Views (XAML) ──binding──► ViewModels                       │
│   App.xaml.cs = composition root (DI) + exception handler    │
└───────────────▲──────────────────────────▲───────────────────┘
                │                          │
┌───────────────┴──────────────┐  ┌────────┴────────────────────┐
│ Application (use-case'ler)   │  │ Infrastructure (dış dünya)  │
│  Services + ViewModels       │◄─┤  SQLite, JSON, Process,     │
│  Abstractions (arayüzler)    │  │  Emülatör adapter'ları,     │
└───────────────▲──────────────┘  │  PlatformDetector, Plugin   │
                │                  └────────────▲────────────────┘
┌───────────────┴──────────────────────────────┴───────────────┐
│ Domain  (Entities, Enums, IEmulatorBackend) — saf, IO yok     │
└──────────────────────────────────────────────────────────────┘
```

- **Domain** hiçbir katmanı bilmez. Entity'ler POCO'dur.
- **Application** sadece Domain + kendi arayüzlerini bilir; somut IO bilmez.
- **Infrastructure** Application arayüzlerini uygular (DIP).
- **App** her şeyi birleştirir; iş mantığı içermez.

## 2. Servis envanteri

| Servis | Arayüz | Sorumluluk |
|---|---|---|
| `GameScannerService` | `IGameScannerService` | Klasörleri recursive tarar, platform tahmin eder, hash/duplicate hesaplar, repo'ya upsert eder |
| `SqliteLibraryRepository` | `ILibraryRepository` | Games/Platforms/EmulatorConfigs/GameOverrides/LibraryFolders/LaunchHistory CRUD |
| `JsonSettingsService` | `ISettingsService` | `settings.json` yükle/kaydet (atomic), `Current` özelliği |
| `EmulatorLaunchService` | `IEmulatorLaunchService` | Pre-flight doğrulama → argüman üretimi → `Process.Start` → history + play time |
| `ProcessLauncher` | `IProcessLauncher` | `ProcessStartInfo` → `ILaunchedProcess` (testlerde mocklanır) |
| `PlatformDetector` | `IPlatformDetector` | Uzantı + magic byte + dizin yapısı → `PlatformId` + güven |
| `PartialFileHasher` | `IFileHasher` | Büyük ISO'lar için hızlı parmak izi (boyut + ilk/son 512 KB) |
| `EmulatorBackendFactory` | `IEmulatorBackendFactory` | Platform → `IEmulatorBackend` (built-in + plugin) |
| `CoverImageService` | `ICoverImageService` | Kullanıcının seçtiği görseli `Metadata/<id>/cover.*` altına kopyalar |
| `BiosCheckService` | — | Platform başına BIOS/firmware yolu varlık kontrolü (sadece kontrol) |
| `LibraryMaintenanceService` | — | Kayıp dosyalar, içe aktarılmamış dosyalar, yeniden tarama |
| `FileLoggingService` | `ILoggingService` | Günlük dosya log'u + rotation |
| `PluginLoader` | `IPluginLoader` | `plugins/*/plugin.json` okur, hash doğrular, backend üretir |

## 3. Ana akışlar

### 3.1 Tarama

```
UI (Tools/Library → Rescan)
  └► IGameScannerService.ScanAsync(folders, progress, ct)
       ├► PlatformDetector.DetectAsync(path, hint)      → PlatformId + confidence
       ├► IFileHasher.ComputeAsync(path)                → "size:sha256head:sha256tail"
       ├► ILibraryRepository.GetGameByPathAsync(path)   → var mı?
       │     var yoksa: GetByHashAsync(hash) → duplicate mu?
       └► ILibraryRepository.UpsertGameAsync(game)
  └► ScanResult { Added, Updated, Unchanged, Duplicates, NeedsReview, Errors }
  └► UI: sonuç özeti + "platformu belirsiz" listesi (kullanıcı düzeltir)
```

### 3.2 Başlatma

```
UI (Launch / Enter)
  └► IEmulatorLaunchService.ValidateAsync(game)
       ├► ortak kontroller : exe yolu var mı? oyun dosyası var mı? BIOS yolu tanımlı mı?
       └► backend.Validate(ctx) : platforma özel (ör. PS3: EBOOT.BIN / PS3_GAME)
       → LaunchValidation { Issues: [Error|Warning] }
  └► hata varsa → UI'da engelleyici dialog; sadece uyarı varsa → devam (log'a yazılır)
  └► IEmulatorBackendFactory.Get(platformId) → backend
  └► backend.BuildArguments(ctx)   (şablon token'ları: {game} {fullscreen} {extra} {bios})
  └► IProcessLauncher.Start(psi)   (UseShellExecute=false, WorkingDirectory=exe dizini)
  └► LaunchHistory insert + Game.LastPlayedAt güncelle
  └► arka planda WaitForExitAsync → Game.PlayTimeMinutes += süre, history kapat
```

### 3.3 Ayarlar

```
App startup → JsonSettingsService.LoadAsync() → AppSettings (singleton)
             → DbInitializer.InitializeAsync() (şema + tohum Platforms)
Settings kaydet → geçici dosyaya yaz → File.Move(overwrite:true) (atomic)
```

## 4. MVVM kuralları

- ViewModel'ler `ObservableObject` (CommunityToolkit) türevlidir; `[ObservableProperty]` ile alan üretimi.
- Her komut `RelayCommand`/`AsyncRelayCommand`; `CanExecute` mümkünse bildirilir.
- **View, arka planda servis çağırmaz**; sadece komut tetikler.
- Liste öğeleri `GameItemViewModel` ile sarılır (favori/kapak değişimi anında yansır).
- Uzun işlemler `AsyncRelayCommand` + `IsBusy` + `IProgress<T>`; UI thread'i bloke edilmez.
- Dialog'lar (`OpenFileDialog`, `FolderBrowserDialog`) `IDialogService` arkasında; VM test edilebilir kalır.
- Tema değişimi `ThemeService` ile yapılır (App.xaml.cs `MergedDictionaries` değiştirir).

## 5. Hata / exception stratejisi

| Seviye | Davranış |
|---|---|
| Domain / Application | Anlamlı exception fırlat (`InvalidOperationException`, `IOException`); yakalama UI'da |
| Infrastructure | Sarmalama: `catch (SqliteException ex) → throw new DataAccessException("...", ex)` |
| Komut (VM) | `try/catch` → `ILoggingService.Error` + kullanıcıya okunur mesaj (`UserMessage` özelliği) |
| Uygulama geneli | `App.DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` → log + "Beklenmeyen hata" dialog'u → uygulama kapanmadan devam etmeye çalışır |
| Log formatı | `2026-10-04 21:14:03.221 [ERR] GameShelf.Emulators.DuckStationBackend - mesaj` + exception |

Log dosyası: `%LOCALAPPDATA%\GameShelf\logs\app-yyyyMMdd.log`, son 7 gün tutulur.
Her launch ayrıca `LaunchHistory` tablosuna yazılır (başarı/başarısız + argümanlar).

## 6. Konfigürasyon (AppSettings)

```jsonc
{
  "theme": "Dark",                 // "Dark" | "Light"
  "accent": "Ember",
  "libraryViewMode": "Grid",       // "Grid" | "List"
  "sortBy": "Title",
  "showUnsupportedPlatforms": true,
  "confirmBeforeLaunch": true,
  "minimizeOnLaunch": true,
  "databasePath": "%LOCALAPPDATA%\\GameShelf\\library.db",
  "metadataPath": "%LOCALAPPDATA%\\GameShelf\\Metadata",
  "pluginPath":  "%LOCALAPPDATA%\\GameShelf\\plugins",
  "biosPaths": { "Ps1": "D:\\Emu\\BIOS", "Ps2": "D:\\Emu\\PCSX2\\bios", "Ps3": "D:\\Emu\\RPCS3\\dev_flash" },
  "extraScanExtensions": [ ".pbp", ".cso" ],
  "lastScanAt": "2026-10-04T18:22:11+03:00",
  "window": { "width": 1280, "height": 820, "isMaximized": false }
}
```

## 7. Threading

- Tüm IO `async`/`await` (`ConfigureAwait(false)` Infrastructure'da).
- `ObservableCollection` **sadece UI thread'inde** güncellenir → VM içinde
  `Application.Current.Dispatcher.InvokeAsync` gerekirse kullanılır.
- Tarama iptali: `CancellationTokenSource` (UI'da "İptal" butonu).
- Büyük klasörlerde tarama tek thread'de ilerler (disk IO zaten darboğaz);
  hash hesabı `Task.Run` ile arka plana alınır.
