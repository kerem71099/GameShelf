using GameShelf.Application.Models;

namespace GameShelf.Application.Abstractions;

/// <summary>Yerel dosya log'u. İnternete çıkmaz.</summary>
public interface ILoggingService
{
    string LogDirectory { get; }

    void Log(LogLevel level, string category, string message, Exception? exception = null);

    void Debug(string category, string message);

    void Info(string category, string message);

    void Warning(string category, string message);

    void Error(string category, string message, Exception? exception = null);
}
