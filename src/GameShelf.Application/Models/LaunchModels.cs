using GameShelf.Domain.Models;

namespace GameShelf.Application.Models;

/// <summary>Başlatma öncesi doğrulama sonucu.</summary>
public sealed record LaunchValidation(IReadOnlyList<LaunchIssue> Issues)
{
    public bool HasErrors => Issues.Any(i => i.Severity == Domain.Enums.LaunchIssueSeverity.Error);

    public bool HasWarnings => Issues.Any(i => i.Severity == Domain.Enums.LaunchIssueSeverity.Warning);

    public IEnumerable<LaunchIssue> Errors => Issues.Where(i => i.Severity == Domain.Enums.LaunchIssueSeverity.Error);

    public string Summary => string.Join(Environment.NewLine, Issues.Select(i => $"[{i.Severity}] {i.Message}"));
}

/// <summary>Başlatma denemesinin sonucu.</summary>
public sealed record LaunchOutcome(
    bool Success,
    int? ProcessId,
    string? EmulatorPath,
    string? Arguments,
    string? Message,
    IReadOnlyList<LaunchIssue> Issues)
{
    public static LaunchOutcome Ok(int? processId, string emulatorPath, string arguments, params LaunchIssue[] issues) =>
        new(true, processId, emulatorPath, arguments, null, issues);

    public static LaunchOutcome Fail(string message, params LaunchIssue[] issues) =>
        new(false, null, null, null, message, issues);
}
