using System.Diagnostics;
using System.Text;
using GameShelf.Application.Abstractions;

namespace GameShelf.Infrastructure.OS;

/// <summary>Başlatılan emülatör süreci. Testlerde yerine sahte (fake) implementasyon geçer.</summary>
public sealed class LaunchedProcess : ILaunchedProcess
{
    /// <summary>Tanılama için tutulacak en fazla karakter (emülatör log'u şişmesin).</summary>
    private const int MaxOutputChars = 8192;

    private readonly Process _process;
    private readonly StringBuilder _output = new();
    private readonly object _gate = new();

    public LaunchedProcess(Process process)
    {
        _process = process;
        CaptureOutput();
    }

    public int Id => _process.Id;

    public string StartupOutput
    {
        get
        {
            lock (_gate)
            {
                return _output.ToString();
            }
        }
    }

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return _process.ExitCode;
    }

    /// <summary>
    /// Konsol çıktısını sınırlı bir tampona okur (yalnızca tanılama).
    /// Emülatör GUI uygulaması olduğu için çoğu zaman boş kalır; hata verdiğinde
    /// (örn. bilinmeyen komut satırı parametresi) mesajı buraya düşer.
    /// </summary>
    private void CaptureOutput()
    {
        try
        {
            if (_process.StartInfo.RedirectStandardOutput)
            {
                _process.OutputDataReceived += OnDataReceived;
                _process.BeginOutputReadLine();
            }

            if (_process.StartInfo.RedirectStandardError)
            {
                _process.ErrorDataReceived += OnDataReceived;
                _process.BeginErrorReadLine();
            }
        }
        catch (Exception)
        {
            // çıktı okunamazsa tanılama bilgisi olmadan devam ederiz
        }
    }

    private void OnDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data))
        {
            return;
        }

        lock (_gate)
        {
            if (_output.Length >= MaxOutputChars)
            {
                return;
            }

            _output.AppendLine(e.Data);
        }
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

        // Konsol çıktısını yönlendiriyoruz: emülatör hemen kapanırsa sebebini
        // (kendi yazdığı hata mesajını) gösterebilmek için. Davranışını değiştirmez.
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

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
