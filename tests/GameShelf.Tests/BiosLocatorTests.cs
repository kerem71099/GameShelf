using GameShelf.Application.Services;
using GameShelf.Domain.Enums;
using Xunit;

namespace GameShelf.Tests;

/// <summary>
/// BIOS konum bulucu testleri. Gerçek dosya indirilmez/üretilmez: yalnızca
/// kullanıcının diskinde "zaten var" sayılan örnek dosyaların TESPİTİ sınanır
/// (boyut/ad desenine göre).
/// <para>
/// Not: taşınabilir işareti (PCSX2 <c>portable.ini</c>, DuckStation <c>portable.txt</c>)
/// olan kurulumlarda veri dizini exe'nin yanıdır; testler bu yüzden işareti de yazar
/// ve böylece makinedeki gerçek kurulumdan bağımsız çalışır.
/// </para>
/// </summary>
public sealed class BiosLocatorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Find_Ps2BiosNextToEmulator_IsDetected()
    {
        var biosDirectory = _temp.CreateDirectory("emulator/bios");
        _temp.CreateTextFile("emulator/portable.ini", string.Empty);
        File.WriteAllBytes(Path.Combine(biosDirectory, "SCPH-39001.bin"), new byte[4 * 1024 * 1024]);
        var exePath = _temp.CreateTextFile("emulator/pcsx2-qtx64-avx2.exe", "not a real emulator");

        var found = BiosLocator.Find(PlatformId.Ps2, exePath);

        Assert.Equal(biosDirectory, found);
    }

    [Fact]
    public void Find_Ps1BiosNextToEmulator_IsDetected()
    {
        var biosDirectory = _temp.CreateDirectory("emulator/bios");
        _temp.CreateTextFile("emulator/portable.txt", string.Empty);
        File.WriteAllBytes(Path.Combine(biosDirectory, "scph1001.bin"), new byte[512 * 1024]);
        var exePath = _temp.CreateTextFile("emulator/duckstation-qt-x64.exe", "not a real emulator");

        var found = BiosLocator.Find(PlatformId.Ps1, exePath);

        Assert.Equal(biosDirectory, found);
    }

    [Fact]
    public void RecommendedDirectory_PortableInstall_IsNextToExe()
    {
        _temp.CreateTextFile("emulator/portable.ini", string.Empty);
        var exePath = _temp.CreateTextFile("emulator/pcsx2-qtx64-avx2.exe", "not a real emulator");

        var recommended = BiosLocator.RecommendedDirectory(PlatformId.Ps2, exePath);

        Assert.Equal(Path.Combine(_temp.Path, "emulator", "bios"), recommended);
    }

    [Fact]
    public void RecommendedDirectory_NormalInstall_UsesDocumentsFolder()
    {
        var exePath = _temp.CreateTextFile("emulator/pcsx2-qtx64-avx2.exe", "not a real emulator");

        var recommended = BiosLocator.RecommendedDirectory(PlatformId.Ps2, exePath);

        // PCSX2 kurulum (portable.ini yok) sürümünde veri dizini Belgelerim\PCSX2'dir.
        Assert.EndsWith(Path.Combine("PCSX2", "bios"), recommended ?? string.Empty);
    }

    public void Dispose() => _temp.Dispose();
}
