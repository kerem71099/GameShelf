using GameShelf.Domain.Enums;
using GameShelf.Infrastructure.Platform;
using Xunit;

namespace GameShelf.Tests;

public sealed class PlatformDetectorTests
{
    private readonly PlatformDetector _detector = new();

    [Fact]
    public async Task CueSheet_ShouldBeDetectedAsPs1()
    {
        using var temp = new TempDirectory();
        var cue = temp.CreateTextFile("Tekken 3.cue", "FILE \"Tekken 3.bin\" BINARY\n  TRACK 01 MODE2/2352\n");

        var result = await _detector.DetectAsync(cue);

        Assert.Equal(PlatformId.Ps1, result.PlatformId);
        Assert.True(result.Confidence > 0.5);
    }

    [Fact]
    public async Task Ps3GameFolder_ShouldBeDetectedAsPs3()
    {
        using var temp = new TempDirectory();
        var game = temp.CreateDirectory("NPEB00577");
        Directory.CreateDirectory(System.IO.Path.Combine(game, "PS3_GAME", "USRDIR"));
        File.WriteAllText(System.IO.Path.Combine(game, "PS3_GAME", "USRDIR", "EBOOT.BIN"), "test");

        Assert.True(await _detector.IsGameDirectoryAsync(game));

        var result = await _detector.DetectAsync(game);

        Assert.Equal(PlatformId.Ps3, result.PlatformId);
    }

    [Fact]
    public async Task Chd_ShouldUseFolderHint_AndStayUnknownWithoutHint()
    {
        using var temp = new TempDirectory();
        var chd = temp.CreateTextFile("game.chd", "MComprHD" + new string(' ', 100));

        var withHint = await _detector.DetectAsync(chd, PlatformId.Ps2);
        var withoutHint = await _detector.DetectAsync(chd);

        Assert.Equal(PlatformId.Ps2, withHint.PlatformId);
        Assert.Equal(PlatformId.Unknown, withoutHint.PlatformId);
    }

    [Fact]
    public async Task IsoWithPs2Marker_ShouldBeDetectedAsPs2()
    {
        using var temp = new TempDirectory();
        var iso = temp.CreateTextFile("ffx.iso", new string('\0', 400_000) + "SYSTEM.CNF  BOOT2 = cdrom0:\\SLPS_250.50");

        var result = await _detector.DetectAsync(iso);

        Assert.Equal(PlatformId.Ps2, result.PlatformId);
    }

    [Fact]
    public async Task Pbp_ShouldBeDetectedAsPs1()
    {
        using var temp = new TempDirectory();
        var pbp = temp.CreateTextFile("EBOOT.PBP", "~PSP eboot data");

        var result = await _detector.DetectAsync(pbp);

        Assert.Equal(PlatformId.Ps1, result.PlatformId);
    }

    [Fact]
    public async Task UnknownFile_ShouldReturnUnknown()
    {
        using var temp = new TempDirectory();
        var file = temp.CreateTextFile("readme.txt", "hello");

        var result = await _detector.DetectAsync(file);

        Assert.Equal(PlatformId.Unknown, result.PlatformId);
    }
}
