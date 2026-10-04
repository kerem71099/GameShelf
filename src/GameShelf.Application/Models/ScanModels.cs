using GameShelf.Domain.Enums;

namespace GameShelf.Application.Models;

public sealed record ScanProgress(int Processed, int Found, string? CurrentPath);

public sealed record PlatformDetection(PlatformId PlatformId, double Confidence, string Reason)
{
    public static readonly PlatformDetection Unknown = new(PlatformId.Unknown, 0d, "Tespit edilemedi");
}

public sealed class ScanResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Duplicates { get; set; }
    public int Missing { get; set; }
    public int NeedsReview { get; set; }

    public List<string> Errors { get; } = new();

    public TimeSpan Duration { get; set; }

    public override string ToString() =>
        $"{Added} eklendi · {Updated} güncellendi · {Unchanged} değişmedi · " +
        $"{Duplicates} tekrar · {Missing} kayıp · {NeedsReview} inceleme bekliyor";
}
