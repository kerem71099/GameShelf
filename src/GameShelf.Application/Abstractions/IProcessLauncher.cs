using System.Diagnostics;

namespace GameShelf.Application.Abstractions;

/// <summary>Başlatılan sürecin test edilebilir soyutlaması.</summary>
public interface ILaunchedProcess
{
    int Id { get; }

    /// <summary>
    /// Emülatörün konsola (stdout/stderr) yazdığı satırlar: yalnızca tanılama içindir ve
    /// sınırlı bir tampon tutar. Çoğu emülatör Windows'ta GUI uygulaması olduğu için
    /// genelde boş kalır; hata verdiklerinde ise nedeni buraya yazarlar
    /// (örn. PCSX2 "Unknown parameter").
    /// </summary>
    string StartupOutput { get; }

    Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Process.Start sarmalayıcısı. Launch smoke testlerinde mock'lanır
/// (hiçbir test gerçekten emülatör açmaz).
/// </summary>
public interface IProcessLauncher
{
    ILaunchedProcess Start(ProcessStartInfo startInfo);
}
