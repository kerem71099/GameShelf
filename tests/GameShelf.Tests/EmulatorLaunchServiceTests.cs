using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;
using GameShelf.Application.Services;
using GameShelf.Domain.Entities;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Extensions;
using GameShelf.Domain.Interfaces;
using GameShelf.Infrastructure.Emulators;
using Xunit;

namespace GameShelf.Tests;

/// <summary>
/// Launch smoke testi: emülatör GERÇEKTEN başlatılmaz (IProcessLauncher sahte).
/// Doğrulama kuralları ve üretilen komut satırı test edilir.
/// </summary>
public sealed class EmulatorLaunchServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly InMemoryLibraryRepository _repository = new();
    private readonly FakeProcessLauncher _launcher = new();
    private readonly FakeSettingsService _settings = new();
    private readonly EmulatorLaunchService _service;

    public EmulatorLaunchServiceTests()
    {
        var backends = new IEmulatorBackend[]
        {
            new DuckStationBackend(),
            new Pcsx2Backend(),
            new Rpcs3Backend()
        };

        var factory = new EmulatorBackendFactory(backends, new NullLogger());

        _service = new EmulatorLaunchService(
            _repository, _settings, factory, _launcher, new NullLogger());
    }

    private string FakeEmulatorExe(string name = "duckstation-qt-x64.exe")
        => _temp.CreateTextFile(name, "not a real emulator");

    private Game Game(PlatformId platformId, string? filePath = null) => new()
    {
        Title = "Test Game",
        PlatformId = platformId,
        FilePath = filePath ?? _temp.CreateTextFile("game.cue", "FILE \"game.bin\" BINARY")
    };

    private Task ConfigureAsync(PlatformId platformId, string exePath)
        => _repository.UpsertEmulatorConfigAsync(new EmulatorConfig
        {
            PlatformId = platformId,
            Name = platformId.ToShortName(),
            ExecutablePath = exePath
        });

    [Fact]
    public async Task Launch_WithoutConfiguredEmulator_FailsAndDoesNotStartProcess()
    {
        var game = Game(PlatformId.Ps1);

        var validation = await _service.ValidateAsync(game);
        var outcome = await _service.LaunchAsync(game);

        Assert.True(validation.HasErrors);
        Assert.False(outcome.Success);
        Assert.Equal(0, _launcher.StartCount);
    }

    [Fact]
    public async Task Launch_WithMissingEmulatorFile_Fails()
    {
        var game = Game(PlatformId.Ps1);
        await ConfigureAsync(PlatformId.Ps1, @"C:\does\not\exist.exe");

        var validation = await _service.ValidateAsync(game);

        Assert.Contains(validation.Errors, i => i.Code == "emulator_not_found");
        Assert.False((await _service.LaunchAsync(game)).Success);
        Assert.Equal(0, _launcher.StartCount);
    }

    [Fact]
    public async Task Launch_WithMissingGameFile_Fails()
    {
        var game = Game(PlatformId.Ps1, @"C:\games\silinmis.cue");
        await ConfigureAsync(PlatformId.Ps1, FakeEmulatorExe());

        var validation = await _service.ValidateAsync(game);
        var outcome = await _service.LaunchAsync(game);

        Assert.Contains(validation.Errors, i => i.Code == "file_missing");
        Assert.False(outcome.Success);
        Assert.Equal(0, _launcher.StartCount);
    }

    [Fact]
    public async Task Launch_WithoutBios_LaunchesWithWarning()
    {
        var game = Game(PlatformId.Ps1);
        await ConfigureAsync(PlatformId.Ps1, FakeEmulatorExe());

        var validation = await _service.ValidateAsync(game);

        Assert.False(validation.HasErrors);
        Assert.True(validation.HasWarnings);

        var outcome = await _service.LaunchAsync(game);

        Assert.True(outcome.Success);
        Assert.Equal(1, _launcher.StartCount);
        Assert.StartsWith("-batch -fullscreen --", _launcher.LastStartInfo!.Arguments);
    }

    [Fact]
    public async Task Launch_HappyPath_StartsProcessAndWritesHistory()
    {
        var game = Game(PlatformId.Ps1);
        var exe = FakeEmulatorExe();
        await ConfigureAsync(PlatformId.Ps1, exe);

        var outcome = await _service.LaunchAsync(game);

        Assert.True(outcome.Success);
        Assert.Equal(exe, _launcher.LastStartInfo!.FileName);
        Assert.False(_launcher.LastStartInfo.UseShellExecute);
        Assert.Equal(Path.GetDirectoryName(exe), _launcher.LastStartInfo.WorkingDirectory);

        var history = await _repository.GetLaunchHistoryAsync();
        Assert.Single(history);
        Assert.True(history[0].Success);
        Assert.NotNull(game.LastPlayedAt);
    }

    [Fact]
    public async Task Launch_UnsupportedPlatform_IsBlocked()
    {
        var game = Game(PlatformId.Ps4, _temp.CreateTextFile("ps4.bin", "x"));

        var validation = await _service.ValidateAsync(game);

        Assert.True(validation.HasErrors);
        Assert.Equal(0, _launcher.StartCount);
    }

    [Fact]
    public async Task Launch_WhenProcessThrows_ReturnsFailure()
    {
        var game = Game(PlatformId.Ps1);
        await ConfigureAsync(PlatformId.Ps1, FakeEmulatorExe());
        _launcher.ShouldThrow = true;

        var outcome = await _service.LaunchAsync(game);

        Assert.False(outcome.Success);

        var history = await _repository.GetLaunchHistoryAsync();
        Assert.Contains(history, h => !h.Success);
    }

    [Fact]
    public async Task Launch_UsesGameOverrideForFullscreen()
    {
        var game = Game(PlatformId.Ps1);
        await ConfigureAsync(PlatformId.Ps1, FakeEmulatorExe());
        await _repository.UpsertGameOverrideAsync(new GameOverride { GameId = game.Id, Fullscreen = false });

        await _service.LaunchAsync(game);

        Assert.DoesNotContain("-fullscreen", _launcher.LastStartInfo!.Arguments);
    }

    [Fact]
    public async Task Preview_ShowsFullCommandLine()
    {
        var game = Game(PlatformId.Ps1);
        var exe = FakeEmulatorExe();
        await ConfigureAsync(PlatformId.Ps1, exe);

        var preview = await _service.BuildCommandPreviewAsync(game);

        Assert.Equal($"\"{exe}\" -batch -fullscreen -- \"{game.FilePath}\"", preview);
    }

    public void Dispose() => _temp.Dispose();
}
