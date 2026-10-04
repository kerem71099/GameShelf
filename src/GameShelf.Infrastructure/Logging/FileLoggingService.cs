using GameShelf.Application.Abstractions;
using GameShelf.Application.Models;

namespace GameShelf.Infrastructure.Logging;

/// <summary>
/// Günlük dosya log'u: <c>%LOCALAPPDATA%\GameShelf\logs\app-yyyyMMdd.log</c>
/// İnternete çıkmaz; son 7 gün tutulur.
/// </summary>
public sealed class FileLoggingService : ILoggingService
{
    private readonly object _sync = new();
    private readonly int _retentionDays;

    public FileLoggingService(string logDirectory, int retentionDays = 7)
    {
        LogDirectory = logDirectory;
        _retentionDays = retentionDays;
        Directory.CreateDirectory(logDirectory);
        Cleanup();
    }

    public string LogDirectory { get; }

    public void Log(LogLevel level, string category, string message, Exception? exception = null)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Tag(level)}] {category} - {message}";

        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        var file = Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");

        try
        {
            lock (_sync)
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Log yazılamıyorsa uygulamayı düşürme.
        }
    }

    public void Debug(string category, string message) => Log(LogLevel.Debug, category, message);

    public void Info(string category, string message) => Log(LogLevel.Info, category, message);

    public void Warning(string category, string message) => Log(LogLevel.Warning, category, message);

    public void Error(string category, string message, Exception? exception = null) =>
        Log(LogLevel.Error, category, message, exception);

    private static string Tag(LogLevel level) => level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "INF"
    };

    private void Cleanup()
    {
        try
        {
            var cutoff = DateTime.Now.Date.AddDays(-_retentionDays);

            foreach (var file in Directory.EnumerateFiles(LogDirectory, "app-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception)
        {
            // Temizlik başarısız olsa da sorun değil.
        }
    }
}
