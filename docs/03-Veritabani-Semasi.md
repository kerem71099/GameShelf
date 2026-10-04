# 03 — Veritabanı Şeması (SQLite)

Dosya: `%LOCALAPPDATA%\GameShelf\library.db`
Şema kaynağı (tek doğruluk kaynağı): `src/GameShelf.Infrastructure/Data/Schema.cs`

## 1. DDL

```sql
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;
PRAGMA synchronous  = NORMAL;

-- ---------------------------------------------------------------- Platforms
CREATE TABLE IF NOT EXISTS Platforms (
    Id                 INTEGER PRIMARY KEY,        -- Domain.Enums.PlatformId (0=Unknown..5=Ps5)
    Name               TEXT    NOT NULL,           -- "PlayStation 3"
    ShortName          TEXT    NOT NULL,           -- "PS3"
    IsSupported        INTEGER NOT NULL DEFAULT 0, -- 1: launcher destekliyor
    RequiresBios       INTEGER NOT NULL DEFAULT 0,
    DefaultExtensions  TEXT    NOT NULL DEFAULT '',-- ".iso,.chd" (noktalı, küçük harf)
    SupportsDirectory  INTEGER NOT NULL DEFAULT 0, -- PS3 gibi klasör hedefi
    AccentColorHex     TEXT    NOT NULL DEFAULT '#6A6F76',
    SortOrder          INTEGER NOT NULL DEFAULT 100
);

-- ------------------------------------------------------------ LibraryFolders
CREATE TABLE IF NOT EXISTS LibraryFolders (
    Id             TEXT    PRIMARY KEY,            -- Guid (metin, büyük/küçük harf duyarsız karşılaştırma)
    Path           TEXT    NOT NULL UNIQUE,        -- normalize edilmiş tam yol
    PlatformHint   INTEGER NULL,                   -- Platforms.Id (opsiyonel ipucu)
    Recursive      INTEGER NOT NULL DEFAULT 1,
    IsEnabled      INTEGER NOT NULL DEFAULT 1,
    LastScannedAt  TEXT    NULL                    -- ISO-8601 (yyyy-MM-ddTHH:mm:ssK)
);

-- -------------------------------------------------------------------- Games
CREATE TABLE IF NOT EXISTS Games (
    Id              TEXT    PRIMARY KEY,           -- Guid
    Title           TEXT    NOT NULL,
    SortTitle       TEXT    NOT NULL,              -- sıralama için normalize ("metal gear solid 3")
    PlatformId      INTEGER NOT NULL DEFAULT 0,    -- 0 = Unknown → kullanıcı düzeltir
    FilePath        TEXT    NOT NULL UNIQUE,       -- dosya veya (PS3 için) klasör
    FileSizeBytes   INTEGER NOT NULL DEFAULT 0,
    ContentHash     TEXT    NULL,                  -- "size:head:tail" parmak izi
    Region          TEXT    NULL,                  -- "EU"/"US"/"JP" (elle)
    CoverPath       TEXT    NULL,                  -- Metadata/<id>/cover.png
    Notes           TEXT    NULL,                  -- kullanıcı notu
    IsFavorite      INTEGER NOT NULL DEFAULT 0,
    IsMissing       INTEGER NOT NULL DEFAULT 0,    -- dosya diskte bulunamadı
    AddedAt         TEXT    NOT NULL,
    LastPlayedAt    TEXT    NULL,
    PlayTimeMinutes INTEGER NOT NULL DEFAULT 0,
    LibraryFolderId TEXT    NULL REFERENCES LibraryFolders(Id) ON DELETE SET NULL,
    FOREIGN KEY (PlatformId) REFERENCES Platforms(Id) ON DELETE RESTRICT
);

-- ----------------------------------------------------------- EmulatorConfigs
CREATE TABLE IF NOT EXISTS EmulatorConfigs (
    Id                TEXT    PRIMARY KEY,
    PlatformId        INTEGER NOT NULL UNIQUE,     -- platform başına tek yapılandırma
    Name              TEXT    NOT NULL,            -- "DuckStation"
    ExecutablePath    TEXT    NULL,
    WorkingDirectory  TEXT    NULL,
    ArgumentTemplate  TEXT    NULL,                -- null → backend'in varsayılanı
    DefaultFullscreen INTEGER NOT NULL DEFAULT 1,
    ExtraArguments    TEXT    NULL,                -- şablonda {extra} yerine geçer
    Notes             TEXT    NULL,
    UpdatedAt         TEXT    NOT NULL,
    FOREIGN KEY (PlatformId) REFERENCES Platforms(Id) ON DELETE CASCADE
);

-- ------------------------------------------------------------- GameOverrides
CREATE TABLE IF NOT EXISTS GameOverrides (
    GameId            TEXT    PRIMARY KEY,
    Fullscreen        INTEGER NULL,                -- NULL = varsayılanı kullan
    ExtraArguments    TEXT    NULL,
    ArgumentTemplate  TEXT    NULL,                -- oyun bazlı şablon (opsiyonel)
    EmulatorConfigId  TEXT    NULL REFERENCES EmulatorConfigs(Id) ON DELETE SET NULL,
    Notes             TEXT    NULL,
    UpdatedAt         TEXT    NOT NULL,
    FOREIGN KEY (GameId) REFERENCES Games(Id) ON DELETE CASCADE
);

-- -------------------------------------------------------------- LaunchHistory
CREATE TABLE IF NOT EXISTS LaunchHistory (
    Id            TEXT    PRIMARY KEY,
    GameId        TEXT    NULL REFERENCES Games(Id) ON DELETE SET NULL,
    PlatformId    INTEGER NOT NULL,
    EmulatorPath  TEXT    NULL,
    Arguments     TEXT    NULL,
    StartedAt     TEXT    NOT NULL,
    EndedAt       TEXT    NULL,
    DurationSeconds INTEGER NULL,
    ExitCode      INTEGER NULL,
    Success       INTEGER NOT NULL DEFAULT 1,      -- 0 = başlatma başarısız
    Message       TEXT    NULL,
    FOREIGN KEY (PlatformId) REFERENCES Platforms(Id) ON DELETE RESTRICT
);

-- -------------------------------------------------------------- SchemaMeta
CREATE TABLE IF NOT EXISTS SchemaMeta (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
```

## 2. Index önerileri

```sql
-- Kütüphane listesi: platform + sıralama + favori filtresi
CREATE INDEX IF NOT EXISTS IX_Games_PlatformId        ON Games(PlatformId);
CREATE INDEX IF NOT EXISTS IX_Games_IsFavorite        ON Games(IsFavorite) WHERE IsFavorite = 1;
CREATE INDEX IF NOT EXISTS IX_Games_LastPlayedAt      ON Games(LastPlayedAt DESC);
CREATE INDEX IF NOT EXISTS IX_Games_AddedAt           ON Games(AddedAt DESC);
CREATE INDEX IF NOT EXISTS IX_Games_SortTitle         ON Games(SortTitle);
CREATE INDEX IF NOT EXISTS IX_Games_ContentHash       ON Games(ContentHash);
CREATE INDEX IF NOT EXISTS IX_Games_IsMissing         ON Games(IsMissing) WHERE IsMissing = 1;

-- Geçmiş: son N kayıt
CREATE INDEX IF NOT EXISTS IX_LaunchHistory_StartedAt ON LaunchHistory(StartedAt DESC);
CREATE INDEX IF NOT EXISTS IX_LaunchHistory_GameId    ON LaunchHistory(GameId);

-- Arama için (LIKE '%..%') SQLite'ta index kullanılmaz; 2.000+ oyun için
-- FTS5 opsiyoneldir (v1+):  CREATE VIRTUAL TABLE GamesFts USING fts5(GameId, Title);
CREATE INDEX IF NOT EXISTS IX_Games_Title             ON Games(Title);
```

**Neden bu index'ler?** Uygulamanın gerçek sorgu kalıpları şunlar:
`(PlatformId = ? AND IsFavorite = ?)` + `ORDER BY SortTitle | LastPlayedAt | AddedAt`,
`WHERE ContentHash = ?` (duplicate), `WHERE IsMissing = 1` (Tools), `StartedAt DESC LIMIT 200` (log).
Yazma hacmi çok düşük (yalnızca tarama ve launch), bu yüzden index maliyeti önemsizdir.

## 3. Erişim kalıpları (Dapper)

```csharp
// Upsert (aynı dosya yolu tekrar taranınca güncelle, Kimliği koru)
INSERT INTO Games (Id, Title, SortTitle, PlatformId, FilePath, FileSizeBytes,
                   ContentHash, AddedAt, PlayTimeMinutes, IsFavorite, IsMissing,
                   LibraryFolderId, Region, CoverPath, Notes, LastPlayedAt)
VALUES (@Id, @Title, @SortTitle, @PlatformId, @FilePath, @FileSizeBytes,
        @ContentHash, @AddedAt, 0, 0, 0, @LibraryFolderId, NULL, NULL, NULL, NULL)
ON CONFLICT(FilePath) DO UPDATE SET
    Title           = excluded.Title,      -- kullanıcı düzenlemeleri korunur:
    SortTitle       = excluded.SortTitle,  -- scanner mevcut kaydı güncellerken
    PlatformId      = excluded.PlatformId, -- kullanıcının değerlerini taşır
    FileSizeBytes   = excluded.FileSizeBytes,
    ContentHash     = excluded.ContentHash,
    Region          = excluded.Region,
    CoverPath       = excluded.CoverPath,
    Notes           = excluded.Notes,
    IsFavorite      = excluded.IsFavorite,
    IsMissing       = excluded.IsMissing,
    LastPlayedAt    = excluded.LastPlayedAt,
    PlayTimeMinutes = excluded.PlayTimeMinutes,
    LibraryFolderId = excluded.LibraryFolderId;
```

```csharp
// Filtre + sıralama (sıralama alanı whitelist ile, string birleştirme değil)
var order = filter.SortBy switch {
    GameSort.RecentlyPlayed => "LastPlayedAt DESC",
    GameSort.RecentlyAdded  => "AddedAt DESC",
    GameSort.MostPlayed     => "PlayTimeMinutes DESC",
    _                       => "SortTitle ASC" };
var sql = $"SELECT * FROM Games WHERE (@Platform IS NULL OR PlatformId = @Platform) " +
          $"AND (@FavOnly = 0 OR IsFavorite = 1) " +
          $"AND (@Search IS NULL OR Title LIKE @Search OR FilePath LIKE @Search) " +
          $"ORDER BY {order} LIMIT @Take OFFSET @Skip";
```

## 4. Tohum veri (Platforms)

| Id | ShortName | IsSupported | RequiresBios | DefaultExtensions | SupportsDirectory |
|---|---|---|---|---|---|
| 1 | PS1 | 1 | 1 | `.cue,.bin,.chd,.iso,.pbp,.m3u` | 0 |
| 2 | PS2 | 1 | 1 | `.iso,.chd,.cso,.bin,.cue` | 0 |
| 3 | PS3 | 1 | 1 | *(yok — klasör tabanlı)* | 1 |
| 4 | PS4 | 0 | 0 | *(yok)* | 0 |
| 5 | PS5 | 0 | 0 | *(yok)* | 0 |
| 0 | Unknown | 0 | 0 | *(yok)* | 0 |

> PS4/PS5 satırları yalnızca UI'da "Desteklenmiyor" placeholder'ı göstermek ve plugin
> mimarisine zemin hazırlamak için vardır. Emülasyon sağlanmaz (bkz. `LEGAL.md`).

## 5. Taşınabilirlik / yedekleme

- `VACUUM INTO 'backup.db'` (tek satır, WAL'den bağımsız) → Settings → Gelişmiş → Yedekle.
- Tam taşınabilirlik istenirse `Metadata/` klasörü + `library.db` + `settings.json` birlikte
  kopyalanmalıdır (kapak yolları mutlak tutulur; taşınırsa "Kayıp kapak" uyarısı gösterilir).
