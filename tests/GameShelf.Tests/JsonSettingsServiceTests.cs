using GameShelf.Application.Models;
using GameShelf.Infrastructure.Settings;
using Xunit;

namespace GameShelf.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task MissingFile_CreatesDefaults()
    {
        var path = Path.Combine(_temp.Path, "settings.json");
        var service = new JsonSettingsService(path, new NullLogger());

        var settings = await service.LoadAsync();

        Assert.Equal("Dark", settings.Theme);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var path = Path.Combine(_temp.Path, "settings.json");
        var service = new JsonSettingsService(path, new NullLogger());

        await service.LoadAsync();
        await service.UpdateAsync(s =>
        {
            s.Theme = "Light";
            s.MinimizeOnLaunch = false;
            s.BiosPaths["Ps1"] = @"D:\emu\bios";
            s.ExtraScanExtensions.Add(".pbp");
        });

        var reloaded = new JsonSettingsService(path, new NullLogger());
        var settings = await reloaded.LoadAsync();

        Assert.Equal("Light", settings.Theme);
        Assert.False(settings.MinimizeOnLaunch);
        Assert.Equal(@"D:\emu\bios", settings.BiosPaths["Ps1"]);
        Assert.Contains(".pbp", settings.ExtraScanExtensions);
    }

    [Fact]
    public async Task CorruptFile_FallsBackToDefaults()
    {
        var path = Path.Combine(_temp.Path, "settings.json");
        await File.WriteAllTextAsync(path, "{ this is not json ");

        var service = new JsonSettingsService(path, new NullLogger());
        var settings = await service.LoadAsync();

        Assert.Equal("Dark", settings.Theme);
    }

    public void Dispose() => _temp.Dispose();
}
