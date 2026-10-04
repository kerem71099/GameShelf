using System.Diagnostics;
using GameShelf.Application.Abstractions;

namespace GameShelf.Infrastructure.OS;

/// <summary>Başlatılan emülatör süreci. Testlerde yerine sahte (fake) implementasyon geçer.</summary>
public sealed class LaunchedProcess : ILaunchedProcess
{
    private readonly Process _process;

    public LaunchedProcess(Process process)
    {
        _process = process;
    }

    public int Id => _process.Id;

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return _process.ExitCode;
    }
}

/// <summary>
/// Process.Start sarmalayıcısı. Emülatörü yalnızca kullanıcının verdiği exe ve
/// resmî komut satırı argümanlarıyla başlatır.
/// </summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    public ILaunchedProcess Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Emülatör süreci başlatılamadı: " + startInfo.FileName);
        }

        return new LaunchedProcess(process);
    }
}
