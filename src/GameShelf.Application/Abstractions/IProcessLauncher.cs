using System.Diagnostics;

namespace GameShelf.Application.Abstractions;

/// <summary>Başlatılan sürecin test edilebilir soyutlaması.</summary>
public interface ILaunchedProcess
{
    int Id { get; }

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
