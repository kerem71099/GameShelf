# 02 — UI/UX Tasarım Dili: **"Shelf"**

## 0. Fikir

Neon/oyuncu klişeleri (mor-mor gradient, cam efekti, glow) yok. Bunun yerine **kâğıt & mürekkep**
metaforu: koyu, sıcak gri yüzeyler; tek bir **toprak tonu** vurgu (Ember/terrakota); bilgi
hiyerarşisi tipografi ve boşlukla kurulur, renkle değil. Kartlar rafla dizilmiş kitap sırtı gibi
hizalanır; bu yüzden adı **Shelf**.

- **Ana karakter:** sıcak nötr gri (#141517 koyu / #F4F1EC açık) + **Ember** vurgusu (#D98A5F).
- **Ayırıcı:** gölge değil **1px hairline border**; yükselti hissi yüzey tonu farkıyla.
- **Hareket:** 120 ms ease-out (hover), 180 ms (sayfa geçişi). Zıplama/overshoot yok.
- **Yasaklar:** glow/shadow-heavy kartlar, gradient dolgular, neon metin, >2 vurgu rengi,
  emoji ikonlar (tek renkli geometrik ikon kullanılır).

## 1. Renk token'ları

| Token | Dark | Light | Kullanım |
|---|---|---|---|
| `Brush.WindowBackground` | `#141517` | `#F4F1EC` | pencere zemini |
| `Brush.Surface` | `#1C1E21` | `#FBF9F6` | kart, panel |
| `Brush.SurfaceRaised` | `#24272B` | `#FFFFFF` | hover, dropdown, dialog |
| `Brush.SurfaceSunken` | `#101113` | `#EAE6DF` | input, kod/log alanı |
| `Brush.Border` | `#2E3237` | `#DFD9D0` | hairline |
| `Brush.BorderStrong` | `#3D4248` | `#C7BFB4` | odak/input border |
| `Brush.TextPrimary` | `#EDEAE4` | `#1E1C1A` | başlık, gövde |
| `Brush.TextSecondary` | `#B4AFA6` | `#55504A` | alt bilgi |
| `Brush.TextMuted` | `#7C7772` | `#8A837B` | placeholder, metadata |
| `Brush.Accent` | `#D98A5F` | `#B4603A` | birincil buton, seçim |
| `Brush.AccentHover` | `#E39C74` | `#9C5230` | hover |
| `Brush.AccentForeground` | `#17140F` | `#FFFFFF` | vurgu üzeri metin |
| `Brush.Danger` | `#E06C5A` | `#B4453A` | silme, hata |
| `Brush.Warning` | `#D8A657` | `#8A6414` | BIOS uyarısı |
| `Brush.Success` | `#7FA86B` | `#4F7A3F` | başarılı kontrol |
| `Brush.Hover` | `#FFFFFF` @6% | `#000000` @4% | satır hover |
| `Brush.Selected` | `#D98A5F` @18% | `#B4603A` @12% | seçili öğe |

**Platform kimlik renkleri** (yalnızca küçük "chip" ve sol kenar çizgisinde kullanılır,
kartın tamamı boyanmaz):

| Platform | Dark | Light |
|---|---|---|
| PS1 | `#6E8B9E` (slate blue) | `#4C6B80` |
| PS2 | `#5F6BB0` (indigo) | `#48539B` |
| PS3 | `#8D6A9F` (mulberry) | `#7A548C` |
| PS4 / PS5 (desteklenmiyor) | `#6A6F76` (gri) | `#8A837B` |
| Bilinmeyen | `#6A6F76` | `#8A837B` |

## 2. Tipografi

| Rol | Font | Boyut | Ağırlık | Not |
|---|---|---|---|---|
| Display | Segoe UI Variable Display | 28 / 22 | Semibold | sayfa başlığı |
| Title | Segoe UI Variable Text | 18 | Semibold | bölüm başlığı |
| BodyStrong | Segoe UI | 14 | Semibold | oyun adı |
| Body | Segoe UI | 14 | Regular | gövde |
| Caption | Segoe UI | 12 | Regular | metadata satırı |
| Overline | Segoe UI | 11 | Semibold | **UPPERCASE**, letter-spacing 0.08em (bölüm etiketi) |

- Satır yüksekliği: body 20 px, caption 16 px.
- Sayısal değerlerde tabular alignment (`Typography` → `FontFamily` numeral; WPF'te gerekirse
  `TextBlock` genişliği sabitlenir).

## 3. Ölçek token'ları (4 px grid)

`Space.XS=4 · S=8 · M=12 · L=16 · XL=24 · XXL=32 · XXXL=48`
`Radius.Control=6 · Radius.Card=12 · Radius.Pill=999`
`BorderThickness.Hairline=1`
`Duration.Fast=120ms · Duration.Normal=180ms` (ease-out: `CubicEase EaseOut`)

## 4. Bileşenler

- **Kart (Grid görünümü):** 168×252 kapak (3:4), `Radius.Card`, 1px border, hover'da
  `SurfaceRaised` + 2px yukarı kayma (120 ms). Kapak altında 2 satır: başlık (BodyStrong,
  2 satırda kırpma) + alt satır (platform chip + süre).
- **Liste görünümü:** 44 px satır yüksekliği; sütunlar: ★ / Kapak(40px) / Başlık / Platform /
  Son oynanma / Süre. Zebra yok; hover satırı `Brush.Hover`.
- **Platform chip:** 6 px radius, metin 11 px uppercase, arka plan platform rengi @14%,
  metin platform rengi (dark'ta 1 ton açık).
- **Butonlar:** Primary (vurgu dolgu, 8/16 padding, radius 6), Secondary (saydam + border),
  Ghost (sadece metin + hover). Yükseklik 32 px (kompakt) / 40 px (birincil eylem).
- **Toggle (Grid/List):** segmented control, `SurfaceSunken` zemin + seçili segment `SurfaceRaised`.
- **Input:** 32 px yükseklik, `SurfaceSunken` zemin, odakta `Accent` border + 2 px.
- **Boş durum (empty state):** ikon + 1 cümle açıklama + tek bir birincil eylem
  ("Oyun klasörü ekle").
- **Toast/inline bildirim:** sağ alt, `SurfaceRaised` + sol kenarda 3 px vurgu çizgisi, 3 sn.

## 5. Ekranlar

### 5.1 Library
```
┌────────────────────────────────────────────────────────────────────────────┐
│ Shelf  |  [🔍 Ara................]  Platform▾  ★Favori  Sırala▾  [▣|☰]  ⚙  │
├────────────────────────────────────────────────────────────────────────────┤
│  PS1 24 · PS2 11 · PS3 4                                   [Yeniden tara]   │
│  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐                               │
│  │ cover  │ │ cover  │ │ cover  │ │ cover  │  ...                           │
│  │ Title  │ │ Title  │ │ Title  │ │ Title  │                               │
│  │ PS1 ·  │ │ PS2 ·  │ │ PS1 ·  │ │ PS3 ·  │                               │
│  └────────┘ └────────┘ └────────┘ └────────┘                               │
└────────────────────────────────────────────────────────────────────────────┘
```
- Grid/List toggle, arama (başlık + dosya adı), filtre (platform chip'leri çoklu seçim,
  favori, son oynanan), sıralama (A–Z / Eklenme / Son oynanma / Oynama süresi).
- Kartta sağ tık menüsü: Başlat, Detay, Favori, Kapağı değiştir, Klasörde göster, Kaldır.

### 5.2 Game Details
```
┌────────────────────────────────────────────────────────────────────────────┐
│ ‹ Kütüphane                                                                 │
│  ┌───────────┐   Metal Gear Solid 3        [PS3]  ★                        │
│  │           │   Region: EU   ·   Son: 2026-09-28  ·  Toplam: 12s 40dk     │
│  │  kapak    │   ─────────────────────────────────────────────────────    │
│  │           │   [ ▶ Başlat ]  [Kapağı değiştir]  [Klasörde göster]        │
│  └───────────┘                                                             │
│   Notlar  ┌───────────────────────────────────────────────────────────┐    │
│           │ kullanıcının notları (otomatik kaydedilir, 500 ms debounce)│    │
│           └───────────────────────────────────────────────────────────┘    │
│   Yol      D:\Games\PS3\NPEB00577\PS3_GAME\USRDIR\EBOOT.BIN                │
│   ═══ Bu oyun için override ═══                                            │
│   Tam ekran  ( Varsayılan | Açık | Kapalı )   Ek argümanlar [............] │
│   Argüman şablonu [ --no-gui {fullscreen} {extra} "{game}"      ] [Sıfırla]│
└────────────────────────────────────────────────────────────────────────────┘
```

### 5.3 Settings
Sol dikey sekme: **Kütüphane klasörleri · Emülatörler · Görünüm · Gelişmiş**.
- Kütüphane: klasör listesi + Ekle/Kaldır + klasör başına platform ipucu (opsiyonel).
- Emülatörler: platform kartları (PS1/PS2/PS3) → exe yolu + Gözat, argüman şablonu,
  tam ekran varsayılanı, çalışma dizini, **BIOS/firmware yolu** + "Yalnızca yol kaydedilir" notu.
- Görünüm: tema (Dark/Light/Sistem), vurgu rengi, kart boyutu, yoğunluk.
- Gelişmiş: veritabanı/l metadata yolu, log klasörünü aç, yedekle/geri yükle, sıfırla.

### 5.4 Tools
- **BIOS / firmware kontrolü**: tablo (Platform · Yapılandırıldı · Bulundu · İpucu) + yasal not.
- **Kayıp dosyalar** (DB'de var, diskte yok) → listeden kaldır.
- **İçe aktarılmamış dosyalar** (diskte var, DB'de yok) → seçerek ekle.
- **Yeniden tara** (tüm kütüphane) + ilerleme çubuğu + iptal.
- **Launch log** (son 200 kayıt: zaman · oyun · sonuç · argümanlar) + log dosyasını aç.

## 6. Klavye kısayolları

| Kısayol | Eylem |
|---|---|
| `Ctrl+F` | Aramaya odaklan |
| `Enter` | Seçili oyunu başlat |
| `↑ ↓ ← →` / `PgUp/PgDn` | Grid içinde gezinme |
| `F5` | Kütüphaneyi yeniden tara |
| `Ctrl+D` | Seçili oyunun detayına git |
| `Ctrl+Shift+F` | Favori filtresini aç/kapat |
| `Ctrl+1 / Ctrl+2` | Grid / Liste görünümü |
| `Ctrl+,` | Ayarlar |
| `Ctrl+T` | Temayı değiştir (Dark/Light) |
| `Alt+←` | Detaydan kütüphaneye dön |
| `Esc` | Aramayı temizle / detaydan çık |
| `Space` | Seçili kartı favorilere ekle/çıkar |

Uygulanış: kısayollar `MainWindow.xaml` içinde `Window.InputBindings` + `KeyBinding`
(`Command="{Binding ...}"`). Arama kutusu odaklıyken harf tuşları yazmaya devam eder.

## 7. Erişilebilirlik & detaylar

- Tüm ikonlar `AutomationProperties.Name` ile etiketlenir; butonlar klavye ile gezinir (`IsTabStop`).
- Odak halkası her temada görünür (`Accent` 2 px).
- Kontrast: gövde metni ≥ 4.5:1 (yukarıdaki palet buna göre seçildi).
- Kapak yoksa: kartta oyun başlığının baş harfleri `SurfaceSunken` zemin üzerinde
  (rastgele değil, başlıktan türetilmiş ton) — boş gri kutu değil.
- Uzun başlıklar 2 satır + elips; `Tooltip` ile tam metin.
- Boş/bulunamayan emülatör: "Başlat" yerine **"Emülatör yolu ayarla"** butonu (Settings'e götürür).
