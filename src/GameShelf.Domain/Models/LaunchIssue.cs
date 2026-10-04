using GameShelf.Domain.Enums;

namespace GameShelf.Domain.Models;

/// <summary>Başlatma öncesi doğrulama bulgusu.</summary>
public sealed record LaunchIssue(LaunchIssueSeverity Severity, string Code, string Message)
{
    public static LaunchIssue Error(string code, string message) =>
        new(LaunchIssueSeverity.Error, code, message);

    public static LaunchIssue Warning(string code, string message) =>
        new(LaunchIssueSeverity.Warning, code, message);

    public static LaunchIssue Info(string code, string message) =>
        new(LaunchIssueSeverity.Info, code, message);
}
