using GameShelf.Application.Services;
using GameShelf.Domain.Enums;
using Xunit;

namespace GameShelf.Tests;

/// <summary>
/// BIOS konum bulucu testleri. Gerçek dosya indirilmez/üretilmez: yalnızca
/// kullanıcının diskinde "zaten var" sayılan örnek dosyaların TESPİTİ sınanır
/// (boyut/ad desenine göre).
/// </summary>
public sealed class BiosLocatorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Find_Ps2BiosNextToEmulator_IsDetected()
    {
        var biosDirectory = _temp.CreateDirectory("emulator/bios");
        File.WriteAllBytes(Path.Combine(biosDirectory, "SCPH-39001.bin"), new byte[4 * 1024 * 1024]);
        var exePath = _temp.CreateTextFile("emulator/pcsx2-qtx64-avx2.exe", "not a real emulator");

        var found = BiosLocator.Find(PlatformId.Ps2, exePath);

        Assert.Equal(biosDirectory, found);
    }

    [Fact]
    public void Find_Ps1BiosNextToEmulator_IsDetected()
    {
        var biosDirectory = _temp.CreateDirectory("emulator/bios");
        File.WriteAllBytes(Path.Combine(biosDirectory, "scph1001.bin"), new byte[512 * 1024]);
        var exePath = _temp.CreateTextFile("emulator/duckstation-qt-x64.exe", "not a real emulator");

        var found = BiosLocator.Find(PlatformId.Ps1, exePath);

        Assert.Equal(biosDirectory, found);
    }

    [Fact]
    public void RecommendedDirectory_UsesEmulatorFolder()
    {
        var exePath = _temp.CreateTextFile("emulator/pcsx2-qtx64-avx2.exe", "not a real emulator");

        var recommended = BiosLocator.RecommendedDirectory(PlatformId.Ps2, exePath);

        Assert.Equal(Path.Combine(_temp.Path, "emulator", "bios"), recommended);
    }

    public void Dispose() => _temp.Dispose();
}
