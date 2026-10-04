namespace GameShelf.Infrastructure.Data;

/// <summary>
/// SQLite şeması — uygulamanın tek doğruluk kaynağı.
/// (Ayrıntılı açıklama: docs/03-Veritabani-Semasi.md)
/// </summary>
public static class Schema
{
    public const int CurrentVersion = 1;

    public static readonly string[] Statements =
    [
        """
        CREATE TABLE IF NOT EXISTS Platforms (
            Id                INTEGER PRIMARY KEY,
            Name              TEXT    NOT NULL,
            ShortName         TEXT    NOT NULL,
            IsSupported       INTEGER NOT NULL DEFAULT 0,
            RequiresBios      INTEGER NOT NULL DEFAULT 0,
            DefaultExtensions TEXT    NOT NULL DEFAULT '',
            SupportsDirectory INTEGER NOT NULL DEFAULT 0,
            AccentColorHex    TEXT    NOT NULL DEFAULT '#6A6F76',
            SortOrder         INTEGER NOT NULL DEFAULT 100
        );
        """,

        """
        CREATE TABLE IF NOT EXISTS LibraryFolders (
            Id            TEXT    PRIMARY KEY,
            Path          TEXT    NOT NULL UNIQUE,
            PlatformHint  INTEGER NULL,
            Recursive     INTEGER NOT NULL DEFAULT 1,
            IsEnabled     INTEGER NOT NULL DEFAULT 1,
            LastScannedAt TEXT    NULL
        );
        """,

        """
        CREATE TABLE IF NOT EXISTS Games (
            Id              TEXT    PRIMARY KEY,
            Title           TEXT    NOT NULL,
            SortTitle       TEXT    NOT NULL,
            PlatformId      INTEGER NOT NULL DEFAULT 0,
            FilePath        TEXT    NOT NULL UNIQUE,
            FileSizeBytes   INTEGER NOT NULL DEFAULT 0,
            ContentHash     TEXT    NULL,
            Region          TEXT    NULL,
            CoverPath       TEXT    NULL,
            Notes           TEXT    NULL,
            IsFavorite      INTEGER NOT NULL DEFAULT 0,
            IsMissing       INTEGER NOT NULL DEFAULT 0,
            AddedAt         TEXT    NOT NULL,
            LastPlayedAt    TEXT    NULL,
            PlayTimeMinutes INTEGER NOT NULL DEFAULT 0,
            LibraryFolderId TEXT    NULL REFERENCES LibraryFolders(Id) ON DELETE SET NULL,
            FOREIGN KEY (PlatformId) REFERENCES Platforms(Id) ON DELETE RESTRICT
        );
        """,

        """
        CREATE TABLE IF NOT EXISTS EmulatorConfigs (
            Id                TEXT    PRIMARY KEY,
            PlatformId        INTEGER NOT NULL UNIQUE,
            Name              TEXT    NOT NULL,
            ExecutablePath    TEXT    NULL,
            WorkingDirectory  TEXT    NULL,
            ArgumentTemplate  TEXT    NULL,
            DefaultFullscreen INTEGER NOT NULL DEFAULT 1,
            ExtraArguments    TEXT    NULL,
            Notes             TEXT    NULL,
            UpdatedAt         TEXT    NOT NULL,
            FOREIGN KEY (PlatformId) REFERENCES Platforms(Id) ON DELETE CASCADE
        );
        """,

        """
        CREATE TABLE IF NOT EXISTS GameOverrides (
            GameId           TEXT    PRIMARY KEY,
            Fullscreen       INTEGER NULL,
            ExtraArguments   TEXT    NULL,
            ArgumentTemplate TEXT    NULL,
            EmulatorConfigId TEXT    NULL REFERENCES EmulatorConfigs(Id) ON DELETE SET NULL,
            Notes            TEXT    NULL,
            UpdatedAt        TEXT    NOT NULL,
            FOREIGN KEY (GameId) REFERENCES Games(Id) ON DELETE CASCADE
        );
        """,

        """
        CREATE TABLE IF NOT EXISTS LaunchHistory (
            Id              TEXT    PRIMARY KEY,
            GameId          TEXT    NULL REFERENCES Games(Id) ON DELETE SET NULL,
            PlatformId      INTEGER NOT NULL,
            EmulatorPath    TEXT    NULL,
            Arguments       TEXT    NULL,
            StartedAt       TEXT    NOT NULL,
            EndedAt         TEXT    NULL,
            DurationSeconds INTEGER NULL,
            ExitCode        INTEGER NULL,
            Success         INTEGER NOT NULL DEFAULT 1,
            Message         TEXT    NULL,
            FOREIGN KEY (PlatformId) REFERENCES Platforms(Id) ON DELETE RESTRICT
        );
        """,

        """
        CREATE TABLE IF NOT EXISTS SchemaMeta (
            Key   TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        );
        """,

        // ---- index'ler (gerçek sorgu kalıplarına göre)
        "CREATE INDEX IF NOT EXISTS IX_Games_PlatformId   ON Games(PlatformId);",
        "CREATE INDEX IF NOT EXISTS IX_Games_IsFavorite   ON Games(IsFavorite) WHERE IsFavorite = 1;",
        "CREATE INDEX IF NOT EXISTS IX_Games_LastPlayedAt ON Games(LastPlayedAt DESC);",
        "CREATE INDEX IF NOT EXISTS IX_Games_AddedAt      ON Games(AddedAt DESC);",
        "CREATE INDEX IF NOT EXISTS IX_Games_SortTitle    ON Games(SortTitle);",
        "CREATE INDEX IF NOT EXISTS IX_Games_ContentHash  ON Games(ContentHash);",
        "CREATE INDEX IF NOT EXISTS IX_Games_IsMissing    ON Games(IsMissing) WHERE IsMissing = 1;",
        "CREATE INDEX IF NOT EXISTS IX_Games_Title        ON Games(Title);",
        "CREATE INDEX IF NOT EXISTS IX_LaunchHistory_StartedAt ON LaunchHistory(StartedAt DESC);",
        "CREATE INDEX IF NOT EXISTS IX_LaunchHistory_GameId    ON LaunchHistory(GameId);"
    ];
}
