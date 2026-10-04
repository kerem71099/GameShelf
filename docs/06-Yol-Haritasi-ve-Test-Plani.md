# 06 — Yol Haritası ve Test Planı

## 1. MVP (hedef: çalışan iskelet, ~1 hafta sonu)

**Kapsam**
1. Solution + 4 proje + DI + tek EXE publish (Debug çalışır).
2. SQLite şema + `Players` tohumu + `SqliteLibraryRepository` (CRUD + upsert).
3. `settings.json` + `JsonSettingsService`.
4. `GameScannerService`: klasör ekle, recursive tara, uzantı filtresi, platform tahmini,
   duplicate (hash) tespiti, DB'ye yazma.
5. Library ekranı: grid, arama, platform filtresi, favori.
6. Settings: kütüphane klasörleri + emülatör exe yolları (PS1/PS2/PS3).
7. `EmulatorLaunchService`: doğrulama + `Process.Start` + launch log (DB).
8. Global exception handling + dosya log'u.

**MVP çıkış kriteri:** Kullanıcı bir klasör ekler, oyunlar listelenir, emülatör yolunu verir,
"Başlat"a basar ve oyun açılır. Uygulama internet istemez.

## 2. v1 (kullanılabilir ürün)

- Game Details ekranı: kapak seçme (`Metadata/<id>/cover.png`), başlık/region/not düzenleme,
  yol bilgisi, klasörde göster.
- Oyun bazlı override: tam ekran (varsayılan/açık/kapalı), ek argümanlar, argüman şablonu.
- Liste görünümü + sıralama + "son oynananlar".
- Dark/Light tema + tema kalıcılığı.
- Tools: BIOS yolu kontrolü, kayıp dosyalar, içe aktarılmamış dosyalar, yeniden tara, launch log ekranı.
- Oynama süresi takibi (çıkışta güncelleme) + `LastPlayedAt`.
- Klavye kısayollarının tamamı.
- Plugin loader (manifest-only mod) + PS4/PS5 placeholder ekranı.
- Portable mod (`portable.txt`), yedekleme (`VACUUM INTO`).

## 3. v2 (opsiyonel / sonra)

- Kapak ölçekleme & thumbnail cache (`DecodePixelWidth` ile bellek optimizasyonu).
- Çoklu emülatör profili (aynı platform için 2. emülatör: ör. PS1 için DuckStation + başka).
- `m3u` çoklu disk desteği (disk değiştirme UI).
- Sürükle-bırak ile kapak ekleme, toplu platform düzeltme.
- FTS5 ile hızlı arama (2.000+ oyun).
- MSIX/Inno Setup installer, otomatik güncelleme (offline kuralını bozmadan: manuel kontrol).

## 4. Test planı

### 4.1 Katmanlar

| Tür | Kapsam | Araç |
|---|---|---|
| Unit | `PlatformDetector`, `GameScannerService` (temp klasör), `EmulatorBackendBase.BuildArguments`, `JsonSettingsService`, `CleanTitle`, token替换 | xUnit |
| Unit + fake | `EmulatorLaunchService` (mock `IProcessLauncher`) → **launch smoke test** | xUnit + el yazımı fake |
| Integration | `SqliteLibraryRepository` (gerçek temp `.db`) | xUnit |
| Manual | Gerçek emülatörle 1'er oyun başlatma, tema/klavye, publish sonrası temiz makine | sen |

### 4.2 Test listesi (MVP)

**PlatformDetector**
- [ ] `Game.cue` (içinde `FILE "...bin" BINARY`) → PS1
- [ ] `Something.chd` + klasör ipucu PS2 → PS2; ipucu yok → Unknown
- [ ] `dev_hdd0/game/NPEB00577/PS3_GAME/USRDIR/EBOOT.BIN` → PS3 (dizin hedefi)
- [ ] PS2 ISO (ilk 4 MB'da `SYSTEM.CNF` + `BOOT2`) → PS2
- [ ] `game.pbp` → PS1
- [ ] `random.dat` → Unknown

**GameScannerService** (temp dizin)
- [ ] 10 dosya → 10 kayıt, doğru platform
- [ ] Aynı klasör 2. kez taranınca `Added=0, Updated/Unchanged=10` (duplicate yok)
- [ ] Aynı içerik farklı yolda → 1 kayıt + `Duplicates=1`
- [ ] Alt klasörler taranır (`Recursive=true`), `false` ise taranmaz
- [ ] Silinmiş dosya → tekrar taramada `IsMissing=1`
- [ ] `IProgress<ScanProgress>` bildirimleri artan sırada

**EmulatorBackendBase / adapter'lar**
- [ ] DuckStation: fullscreen açık → `-batch -fullscreen -- "C:\g\a.cue"`
- [ ] DuckStation: fullscreen kapalı + extra `-fastboot` → `-batch -fastboot -- "..."`
- [ ] PCSX2: `--fullscreen -- "D:\ps2\ffx.iso"` / kapalı → `--nofullscreen -- "..."`
- [ ] RPCS3: `--no-gui "E:\RPCS3\dev_hdd0\game\NPUB30780\PS3_GAME\USRDIR\EBOOT.BIN"`
- [ ] Kullanıcı şablonu varsayılanı ezer; boşsa varsayılan döner
- [ ] Boşluklu yol tırnaklanır; fazla boşluklar temizlenir

**EmulatorLaunchService (smoke test, mock process)**
- [ ] exe yolu boş → `Success=false`, hata kodu `emulator_not_configured`, **process başlatılmaz**
- [ ] oyun dosyası yok → `Success=false`, `file_missing`
- [ ] BIOS yolu yok → **sadece uyarı**, launch devam eder
- [ ] Mutlu yol → `Success=true`, doğru `FileName`/`Arguments`/`WorkingDirectory`
- [ ] Launch sonrası `LaunchHistory` + `LastPlayedAt` güncellenir

**Repository (integration)**
- [ ] Upsert aynı `FilePath` → tek satır, Id korunur
- [ ] Filtre: platform / favori / arama / sıralama (4 varyant)
- [ ] Klasör silinince oyunların `LibraryFolderId` NULL olur
- [ ] `LaunchHistory` son 200 kayıt azalan sırada

**Settings**
- [ ] Dosya yok → varsayılanlar; kaydet → tekrar yükle aynı değerler
- [ ] Bozuk JSON → varsayılanlar + log uyarısı (uygulama açılır)
- [ ] Eşzamanlı kaydetme → dosya bozulmaz (atomic write)

### 4.3 Manuel smoke test (her sürümde 10 dk)

1. Temiz `%LOCALAPPDATA%\GameShelf` → uygulama açılır, şema oluşur.
2. 1 PS1 + 1 PS2 + 1 PS3 örnek ekle → hepsi doğru platformda listelenir.
3. Her platform için emülatör yolu ver → Tools → BIOS check → uyarı/OK görünür.
4. Sırayla başlat → oyun açılır, kapatınca süre işlenir.
5. Tema değiştir → tüm ekranlar okunur (kontrast), ayar kalıcı.
6. `PublishSingleFile` çıktısını temiz bir klasöre kopyala → çalışır (SQLite native testi!).
7. Uygulamayı ağ bağlantısı kesikken kullan → hiçbir hata/timeout yok.
