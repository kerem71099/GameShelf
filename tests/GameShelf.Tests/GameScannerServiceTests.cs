using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Application.Services;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using GameShelf.Infrastructure.OS;
using GameShelf.Infrastructure.Platform;
using Xunit;

namespace GameShelf.Tests;

public sealed class GameScannerServiceTests
{
    private static GameScannerService CreateScanner(ILibraryRepository repository, ISettingsService settings)
        => new(repository, new PlatformDetector(), new PartialFileHasher(), settings, new NullLogger());

    [Fact]
    public async Task Scan_ShouldAddAllSupportedFiles()
    {
        using var temp = new TempDirectory();
        var root = temp.CreateDirectory("Games");
        var folder = new LibraryFolder { Path = root, Recursive = true };

        File.WriteAllText(Path.Combine(root, "Tekken 3.cue"), "FILE \"Tekken 3.bin\" BINARY");
        File.WriteAllText(Path.Combine(root, "notes.txt"), "not a game");
        Directory.CreateDirectory(Path.Combine(root, "PS3", "NPEB00577", "PS3_GAME", "USRDIR"));
        File.WriteAllText(Path.Combine(root, "PS3", "NPEB00577", "PS3_GAME", "USRDIR", "EBOOT.BIN"), "x");

        var repository = new InMemoryLibraryRepository();
        var scanner = CreateScanner(repository, new FakeSettingsService());

        var result = await scanner.ScanAsync([folder]);

        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.Duplicates);

        var games = await repository.GetGamesAsync(new LibraryFilter());
        Assert.Contains(games, g => g.PlatformId == PlatformId.Ps1);
        Assert.Contains(games, g => g.PlatformId == PlatformId.Ps3);
        Assert.DoesNotContain(games, g => g.FilePath.EndsWith(".txt"));
    }

    [Fact]
    public async Task Scan_Twice_ShouldNotCreateDuplicates()
    {
        using var temp = new TempDirectory();
        var root = temp.CreateDirectory("Games");
        var folder = new LibraryFolder { Path = root };

        File.WriteAllText(Path.Combine(root, "Metal Gear Solid.iso"), new string('A', 2048));

        var repository = new InMemoryLibraryRepository();
        var scanner = CreateScanner(repository, new FakeSettingsService());

        var first = await scanner.ScanAsync([folder]);
        var second = await scanner.ScanAsync([folder]);

        Assert.Equal(1, first.Added);
        Assert.Equal(0, second.Added);
        Assert.Equal(1, second.Unchanged);
        Assert.Equal(1, (await repository.GetGamesAsync(new LibraryFilter())).Count);
    }

    [Fact]
    public async Task Scan_ShouldDetectDuplicateContentOnDifferentPaths()
    {
        using var temp = new TempDirectory();
        var root = temp.CreateDirectory("Games");
        var folder = new LibraryFolder { Path = root };

        var content = new string('B', 4096);
        File.WriteAllText(Path.Combine(root, "Game A.iso"), content);
        File.WriteAllText(Path.Combine(root, "Game A - kopya.iso"), content);

        var repository = new InMemoryLibraryRepository();
        var scanner = CreateScanner(repository, new FakeSettingsService());

        var result = await scanner.ScanAsync([folder]);

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Duplicates);
    }

    [Fact]
    public async Task Scan_ShouldMarkDeletedFilesAsMissing()
    {
        using var temp = new TempDirectory();
        var root = temp.CreateDirectory("Games");
        var folder = new LibraryFolder { Path = root };

        var file = Path.Combine(root, "Wipeout.iso");
        File.WriteAllText(file, new string('C', 2048));

        var repository = new InMemoryLibraryRepository();
        var scanner = CreateScanner(repository, new FakeSettingsService());

        await scanner.ScanAsync([folder]);
        File.Delete(file);

        var result = await scanner.ScanAsync([folder]);

        Assert.Equal(1, result.Missing);

        var games = await repository.GetGamesAsync(new LibraryFilter());
        Assert.True(games[0].IsMissing);
    }

    [Fact]
    public async Task Scan_ShouldReportProgress()
    {
        using var temp = new TempDirectory();
        var root = temp.CreateDirectory("Games");
        var folder = new LibraryFolder { Path = root };

        File.WriteAllText(Path.Combine(root, "A.iso"), new string('D', 1024));
        File.WriteAllText(Path.Combine(root, "B.iso"), new string('E', 1024));

        var progress = new List<ScanProgress>();
        var scanner = CreateScanner(new InMemoryLibraryRepository(), new FakeSettingsService());

        await scanner.ScanAsync([folder], new Progress<ScanProgress>(progress.Add));

        Assert.NotEmpty(progress);
        Assert.True(progress.Last().Processed >= 2);
    }

    [Fact]
    public async Task Scan_ShouldFlagUnknownPlatformForReview()
    {
        using var temp = new TempDirectory();
        var root = temp.CreateDirectory("Games");
        var folder = new LibraryFolder { Path = root };

        File.WriteAllText(Path.Combine(root, "belirsiz.chd"), "MComprHD" + new string(' ', 64));

        var scanner = CreateScanner(new InMemoryLibraryRepository(), new FakeSettingsService());
        var result = await scanner.ScanAsync([folder]);

        Assert.Equal(1, result.NeedsReview);
    }

    [Fact]
    public void TitleCleaner_ShouldStripSceneTags()
    {
        var title = TitleCleaner.Clean(@"D:\Games\Metal.Gear.Solid.3.(Europe).[SCES-12345].iso");

        Assert.DoesNotContain("SCES", title);
        Assert.Contains("Metal Gear Solid", title);
    }
}
