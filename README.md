# GameShelf — Offline PlayStation Emulator Hub (Windows 10/11)

GameShelf, mevcut emülatörleri (DuckStation / PCSX2 / RPCS3) **tek arayüzden yöneten**, tamamen **offline** çalışan bir Windows launcher (frontend) uygulamasıdır.

> **GameShelf emülatör çekirdeği yazmaz, emülatör dağıtmaz, oyun/BIOS sağlamaz.**
> Uygulama yalnızca kullanıcının **kendi yasal dump'larını** ve **kendi sağladığı** BIOS/firmware dosyalarını yerelden kullanır.
> Ayrıntılı sınırlar: [`LEGAL.md`](LEGAL.md).

## Teknoloji (kısa özet)

| Katman | Seçim | Neden |
|---|---|---|
| UI | **.NET 8 (LTS) + WPF**, MVVM | Windows-only hedef, tek EXE publish, olgun tooling, WinUI 3'e göre daha az hareketli parça |
| MVVM | CommunityToolkit.Mvvm | Source generator ile boilerplate az |
| DI | Microsoft.Extensions.DependencyInjection | `IServiceProvider` ile 30 satırda kurulum |
| Veri | **SQLite (Microsoft.Data.Sqlite) + Dapper** | Şema SQL ile okunabilir, EF Core migration yükü yok |
| Ayarlar | System.Text.Json → `settings.json` | Yeterli, bağımlılık yok |
| Log | Kendi dosya logger'ı | Offline zorunluluğu; basit ve yeterli |
| Test | xUnit (+ NSubstitute opsiyonel) | Çekirdek mantık testi |

Gerekçelerin tamamı: [`docs/00-Teknoloji-Secimi-ve-Proje-Yapisi.md`](docs/00-Teknoloji-Secimi-ve-Proje-Yapisi.md)

## Depo düzeni

```
GameShelf.sln
src/
  GameShelf.Domain/         <- entity, enum, arayüzler (saf, IO yok)
  GameShelf.Application/    <- servisler + ViewModel'ler (use-case katmanı)
  GameShelf.Infrastructure/ <- SQLite, JSON ayar, process launch, emülatör adapter'ları
  GameShelf.App/            <- WPF: Views, Themes, App.xaml, DI composition root
tests/
  GameShelf.Tests/          <- unit test + launch smoke test (mock process)
docs/                       <- tasarım, şema, yol haritası, adım adım rehber
```

## Derleme & çalıştırma (Windows)

```powershell
# 1) SDK: .NET 8 SDK + Visual Studio 2022 (".NET desktop development" iş yükü)
dotnet --version

# 2) Restore + build + test
dotnet restore GameShelf.sln
dotnet build   GameShelf.sln -c Debug
dotnet test    tests/GameShelf.Tests/GameShelf.Tests.csproj

# 3) Çalıştır
dotnet run --project src/GameShelf.App/GameShelf.App.csproj
```

## Tek EXE dağıtım

```powershell
# Framework-dependent single file (~5 MB, hedefte .NET 8 Runtime gerekir)
dotnet publish src/GameShelf.App/GameShelf.App.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# Self-contained single file (~80 MB, runtime gerekmez)
dotnet publish src/GameShelf.App/GameShelf.App.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

> `IncludeNativeLibrariesForSelfExtract=true` şart: SQLite native (`e_sqlite3.dll`) tek dosya içinden
> çalışma zamanında geçici klasöre açılır.

Opsiyonel installer: MSIX (Windows Application Packaging Project) veya Inno Setup / WiX —
detaylar `docs/00` içinde.

## Dokümantasyon

| Dosya | İçerik |
|---|---|
| [`docs/00-Teknoloji-Secimi-ve-Proje-Yapisi.md`](docs/00-Teknoloji-Secimi-ve-Proje-Yapisi.md) | Teknoloji seçimi + gerekçe, NuGet listesi, publish |
| [`docs/01-Mimari.md`](docs/01-Mimari.md) | Katmanlar, servisler, veri akışı, hata yönetimi |
| [`docs/02-UI-UX-Tasarim-Dili.md`](docs/02-UI-UX-Tasarim-Dili.md) | "Shelf" tasarım dili, token'lar, ekranlar, kısayollar |
| [`docs/03-Veritabani-Semasi.md`](docs/03-Veritabani-Semasi.md) | SQLite DDL + index önerileri |
| [`docs/04-Emulator-Entegrasyonu.md`](docs/04-Emulator-Entegrasyonu.md) | DuckStation/PCSX2/RPCS3 CLI, argüman şablonları |
| [`docs/05-Plugin-Mimarisi.md`](docs/05-Plugin-Mimarisi.md) | Manifest tabanlı plugin taslağı (PS4/PS5 placeholder) |
| [`docs/06-Yol-Haritasi-ve-Test-Plani.md`](docs/06-Yol-Haritasi-ve-Test-Plani.md) | MVP → v1 → v2, test planı |
| [`docs/07-Adim-Adim-Rehber.md`](docs/07-Adim-Adim-Rehber.md) | **Sıfırdan, adım adım uygulama rehberi** |

## Platform desteği

| Platform | Durum | Not |
|---|---|---|
| PS1 | ✅ Launcher | DuckStation (kullanıcı exe yolunu verir) |
| PS2 | ✅ Launcher | PCSX2 |
| PS3 | ✅ Launcher | RPCS3 (klasör/EBOOT.BIN yapıları) |
| PS4 / PS5 | ⛔ Desteklenmiyor | UI'da placeholder + plugin arayüzü hazır; emülasyon sağlanmaz |
