using GameShelf.Application.Models;
using GameShelf.Domain.Enums;
using GameShelf.Domain.Interfaces;

namespace GameShelf.Application.Abstractions;

/// <summary>
/// Büyük dosyalar (ISO/CHD) için hızlı parmak izi.
/// Format: <c>boyut:ilkParcaHash:sonParcaHash</c>. Duplicate tespiti içindir.
/// </summary>
public interface IFileHasher
{
    Task<string> ComputeAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>Uzantı + magic byte + dizin yapısına göre platform tahmini.</summary>
public interface IPlatformDetector
{
    /// <summary>Taranacak tüm bilinen uzantılar (platform default'ları + kullanıcı ekleri).</summary>
    IReadOnlyList<string> KnownExtensions { get; }

    Task<PlatformDetection> DetectAsync(string path, PlatformId? hint = null, CancellationToken cancellationToken = default);

    /// <summary>PS3 gibi klasör yapılarında oyun klasörü mü? (PS3_GAME / PS3_DISC.SFB / PARAM.SFO+USRDIR)</summary>
    Task<bool> IsGameDirectoryAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>Platform → backend eşlemesi (built-in adapter'lar + manifest plugin'leri).</summary>
public interface IEmulatorBackendFactory
{
    IEmulatorBackend? Get(PlatformId platformId);

    IReadOnlyList<IEmulatorBackend> All { get; }

    void Register(IEmulatorBackend backend);
}

/// <summary>Kullanıcının seçtiği kapak görselini uygulama metadata klasörüne kopyalar.</summary>
public interface ICoverImageService
{
    /// <summary>Kaynağı <c>Metadata/&lt;gameId&gt;/cover.&lt;uzantı&gt;</c> altına kopyalar; hedef yolu döner.</summary>
    Task<string?> ImportAsync(Guid gameId, string sourceFile, CancellationToken cancellationToken = default);

    bool Delete(Guid gameId);
}

/// <summary>Dosya/folder dialogları (WPF bağımlılığını VM'den uzak tutar).</summary>
public interface IDialogService
{
    string? OpenFile(string title, string filter, string? initialDirectory = null);

    string? OpenFolder(string description, string? initialDirectory = null);

    bool Confirm(string title, string message);

    void ShowMessage(string title, string message);
}

/// <summary>UI thread'ine marshalling (testlerde senkron çalışan sahte implementasyon kullanılır).</summary>
public interface IDispatcher
{
    Task InvokeAsync(Action action);

    void Invoke(Action action);
}

/// <summary>Explorer'da gösterme gibi kabuk işlemleri.</summary>
public interface IShellService
{
    void RevealInExplorer(string path);

    void OpenFolder(string path);

    /// <summary>Resmî dokümantasyon/bilgi sayfasını varsayılan tarayıcıda açar (indirme bağlantısı değil).</summary>
    void OpenUrl(string url);
}

/// <summary>Plugin klasöründen backend yükleme (manifest + opsiyonel imzalı DLL).</summary>
public interface IPluginLoader
{
    IReadOnlyList<IEmulatorBackend> LoadAll(string pluginRoot);
}
