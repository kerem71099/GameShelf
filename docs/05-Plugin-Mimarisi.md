# 05 — Plugin Mimarisi (taslak, güvenli ve basit)

## 1. Amaç ve sınırlar

GameShelf'in çekirdeği **PS1/PS2/PS3 launcher**'dır. PS4/PS5 (ve ileride başka platformlar)
için **emülasyon sağlanmaz**; yalnızca `IEmulatorBackend` sözleşmesini uygulayan üçüncü taraf
bir plugin'in **kullanıcı kendi kurulumunu yaptıysa** arayüze entegre olmasına izin verilir.

**Kesin yasaklar (plugin'ler için de geçerli):**
- Oyun/ROM/ISO indirme, linkleme, scraping, torrent.
- BIOS/firmware/key sağlama veya indirme.
- DRM/koruma atlatma, decrypt, crack, lisans/serial üretimi.
- Kendi başına ağ erişimi (offline garantisi).

Bu yasaklar manifest'te beyan edilir (`capabilities`) ve zorunlu alan
`declaresNoDrmBypass: true` **olmadan plugin yüklenmez**.

## 2. Neden DLL + manifest (sadece manifest değil)?

- Sadece manifest (JSON → süreç başlatma betiği) yaklaşımı esnek ama sınırlı:
  koşullu argüman üretimi, doğrulama mantığı yazılamaz.
- Saf DLL yüklemesi ise güvensiz: imzasız rastgele kod.
- **Orta yol:** manifest zorunlu + DLL **opsiyonel**. Manifest yalnızca şablon/uzantı
  tanımlıyorsa ek kod çalıştırılmaz (`CodeFree = true`). DLL varsa:
  1. manifest'teki `sha256` ile dosya doğrulanır,
  2. sadece `IEmulatorBackend` uygulayan tek bir tip örneklenir,
  3. tip `AssemblyLoadContext` içinde yüklenir (kaldırılabilir, `isCollectible: true`),
  4. ağ/IO yetkisi verilmez (`capabilities` boşsa loader, `NoNetwork` politikasını kaydeder).

## 3. `plugin.json` şeması

```jsonc
{
  "schemaVersion": 1,
  "id": "ps4launcher.example",          // küçük harf, noktalı, benzersiz
  "name": "Example PS4 Launcher",
  "version": "1.0.0",
  "author": "you",
  "platformId": 4,                      // Domain.Enums.PlatformId
  "displayName": "PS4 (community)",
  "contract": { "interface": "IEmulatorBackend", "version": 1 },

  // ---- zorunlu yasal beyanlar
  "declaresNoDrmBypass": true,
  "declaresNoRomDistribution": true,
  "declaresOfflineOnly": true,

  // ---- yetkiler (hepsi kapalı gelir; açılan her yetki UI'da kullanıcıya gösterilir)
  "capabilities": {
    "network": false,
    "fileSystemOutsideLibrary": false,
    "executeArbitraryProcess": true     // backend zaten tek bir süreç başlatır
  },

  // ---- kod yüklemeden çalışan mod (önerilen)
  "codeFree": true,
  "executableHint": "C:\\Program Files\\Example\\example.exe",
  "supportedExtensions": [ ".pkg", ".iso" ],
  "supportsDirectoryTargets": false,
  "defaultArgumentTemplate": "{fullscreen} {extra} \"{game}\"",
  "fullscreenArgument": "--fullscreen",
  "noFullscreenArgument": "",
  "requiresBios": false,
  "biosHint": "",

  // ---- DLL modu (opsiyonel; codeFree=false ise zorunlu)
  "assembly": {
    "file": "ExamplePs4Backend.dll",
    "typeName": "ExamplePs4.Backends.ExamplePs4Backend",
    "sha256": "3f1c... (küçük harf hex, 64 karakter)"
  }
}
```

Kurulum yeri: `%LOCALAPPDATA%\GameShelf\plugins\<pluginId>\plugin.json`.

## 4. Yükleme akışı (`Infrastructure/Plugins/PluginLoader.cs`)

```
LoadAll(pluginRoot)
 ├ her alt klasörde plugin.json var mı?
 ├ JSON parse + schemaVersion == 1 ?
 ├ id/platformId/typeName doğrulama (regex: ^[a-z0-9.\-]+$)
 ├ declaresNoDrmBypass && declaresNoRomDistribution && declaresOfflineOnly == true ?
 │     hayır → reddet, log'a "policy_violation" yaz, UI'da uyarı göster
 ├ capabilities.network == true ? → reddet (offline garantisi; v1'de hiç izin verilmez)
 ├ codeFree == true → ManifestOnlyBackend üret (kod çalıştırma yok)
 └ assembly varsa:
      ├ SHA-256 doğrula (manifest ile birebir)
      ├ dosya yolu plugin klasörü içinde mi (path traversal engeli)?
      ├ AssemblyLoadContext(isCollectible: true) → LoadFromAssemblyPath
      ├ tip bul: typeName, IEmulatorBackend implemente ediyor mu, parametresiz ctor var mı?
      ├ Activator.CreateInstance → IEmulatorBackend
      └ hata olursa: yakala, logla, diğer plugin'lere devam et (bir plugin uygulamayı çökertmez)
```

## 5. Arayüz kararlılığı

- `IEmulatorBackend` **v1** olarak dondurulur. Kırıcı değişiklik gerekirse `contract.version`
  artırılır; loader sürüm uyuşmazlığında plugin'i **yüklemez** (sessizce değil, UI'da uyarıyla).
- Plugin'lere `IServiceProvider` **verilmez**; yalnızca `EmulatorLaunchContext` verilir.
  Böylece plugin veritabanına/ayarlara doğrudan erişemez.
- Plugin'in `Validate`/`BuildArguments` çağrıları `try/catch` içinde yapılır; hata → launch iptal + log.

## 6. UI yansıması

- `Platforms.IsSupported = 1` olan bir plugin platformu eklendiğinde Library filtresinde chip görünür.
- Settings → Emülatörler'e plugin kartı eklenir: ad, sürüm, yazar, yetkiler, "Kaldır".
- Tools → "Plugin'ler" bölümü: yüklü plugin listesi, doğrulama sonucu (OK / reddedildi + sebep).

## 7. Güvenlik kontrol listesi (her yeni plugin özelliğinde gözden geçir)

- [ ] Manifest alanları whitelist ile mi doğrulanıyor (bilinmeyen alan yok sayılıyor mu)?
- [ ] `assembly.file` yolu plugin klasörü dışına çıkabiliyor mu? (`Path.GetFullPath` + `StartsWith`)
- [ ] SHA-256 uyuşmazlığında yükleme duruyor mu?
- [ ] `capabilities.network` her koşulda reddediliyor mu?
- [ ] Bir plugin'in exception'ı uygulamayı çökertmiyor mu?
- [ ] Plugin UI'a metin sağlıyorsa XAML enjeksiyonu mümkün mü? (sağlamıyor; sabit metin kullanılır)
