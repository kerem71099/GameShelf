using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Interfaces;
using GameShelf.Domain.Models;
using GameShelf.Infrastructure.Emulators;
using Xunit;

namespace GameShelf.Tests;

/// <summary>Argüman üretimi (komut satırı) testleri. Hiçbir emülatör gerçekten başlatılmaz.</summary>
public sealed class EmulatorBackendTests
{
    private static EmulatorLaunchContext Context(
        string gamePath,
        PlatformId platformId,
        string? template = null,
        bool fullscreen = true,
        string? extra = null,
        string? bios = null,
        GameOverride? gameOverride = null)
        => new()
        {
            Game = new Game { Title = "Test", PlatformId = platformId, FilePath = gamePath },
            Emulator = new EmulatorConfig { PlatformId = platformId, ExecutablePath = @"C:\emu\emu.exe", ArgumentTemplate = template },
            GameOverride = gameOverride,
            Fullscreen = fullscreen,
            ExtraArguments = extra,
            BiosPath = bios,
            PlatformSupported = true
        };

    [Fact]
    public void DuckStation_DefaultTemplate_Fullscreen()
    {
        IEmulatorBackend backend = new DuckStationBackend();

        var args = backend.BuildArguments(Context(@"C:\Games\Tekken 3.cue", PlatformId.Ps1));

        Assert.Equal("-batch -fullscreen -- \"C:\\Games\\Tekken 3.cue\"", args);
    }

    [Fact]
    public void DuckStation_NoFullscreen_WithExtra()
    {
        IEmulatorBackend backend = new DuckStationBackend();

        var args = backend.BuildArguments(
            Context(@"C:\Games\Tekken 3.cue", PlatformId.Ps1, fullscreen: false, extra: "-fastboot"));

        Assert.Equal("-batch -fastboot -- \"C:\\Games\\Tekken 3.cue\"", args);
    }

    [Fact]
    public void Pcsx2_FullscreenFlags()
    {
        IEmulatorBackend backend = new Pcsx2Backend();

        var fullscreen = backend.BuildArguments(Context(@"D:\ps2\Final Fantasy X.iso", PlatformId.Ps2));
        var windowed = backend.BuildArguments(Context(@"D:\ps2\Final Fantasy X.iso", PlatformId.Ps2, fullscreen: false));

        Assert.Equal("--fullscreen -- \"D:\\ps2\\Final Fantasy X.iso\"", fullscreen);
        Assert.Equal("--nofullscreen -- \"D:\\ps2\\Final Fantasy X.iso\"", windowed);
    }

    [Fact]
    public void Rpcs3_UsesNoGuiAndQuotedFolder()
    {
        IEmulatorBackend backend = new Rpcs3Backend();

        var args = backend.BuildArguments(
            Context(@"E:\RPCS3\dev_hdd0\game\NPUB30780\PS3_GAME\USRDIR\EBOOT.BIN", PlatformId.Ps3));

        Assert.Equal("--no-gui \"E:\\RPCS3\\dev_hdd0\\game\\NPUB30780\\PS3_GAME\\USRDIR\\EBOOT.BIN\"", args);
    }

    [Fact]
    public void CustomTemplate_OverridesDefault()
    {
        IEmulatorBackend backend = new Pcsx2Backend();

        var args = backend.BuildArguments(
            Context(@"D:\ps2\game.iso", PlatformId.Ps2, template: "-state 3 {fullscreen} -- \"{game}\""));

        Assert.Equal("-state 3 --fullscreen -- \"D:\\ps2\\game.iso\"", args);
    }

    [Fact]
    public void PathWithoutQuotesInTemplate_IsQuotedAutomatically()
    {
        IEmulatorBackend backend = new DuckStationBackend();

        var args = backend.BuildArguments(
            Context(@"C:\My Games\Tekken 3.cue", PlatformId.Ps1, template: "-batch {game}"));

        Assert.Equal("-batch \"C:\\My Games\\Tekken 3.cue\"", args);
    }

    [Fact]
    public void MissingBios_ProducesWarningOnly()
    {
        IEmulatorBackend backend = new DuckStationBackend();

        var issues = backend.Validate(Context(@"C:\Games\a.cue", PlatformId.Ps1)).ToList();

        Assert.Contains(issues, i => i.Code == "bios_not_configured" && i.Severity == LaunchIssueSeverity.Warning);
        Assert.DoesNotContain(issues, i => i.Severity == LaunchIssueSeverity.Error);
    }

    [Fact]
    public void UnexpectedExtension_ProducesWarning()
    {
        IEmulatorBackend backend = new Pcsx2Backend();

        var issues = backend.Validate(Context(@"D:\ps2\game.weird", PlatformId.Ps2)).ToList();

        Assert.Contains(issues, i => i.Code == "extension_unexpected");
    }
}
