using GameShelf.Domain.Enums;

namespace GameShelf.Application.Models;

/// <summary>
/// BIOS/firmware yolu kontrol sonucu.
/// GameShelf bu dosyaları SAĞLAMAZ; yalnızca kullanıcının verdiği yolun varlığını kontrol eder.
/// </summary>
/// <param name="TargetDirectory">BIOS'un konması gereken klasör (emülatörün kendi klasörü; boş olabilir).</param>
public sealed record BiosStatus(
    PlatformId PlatformId,
    string PlatformName,
    bool Required,
    bool Configured,
    bool Exists,
    string? Path,
    string Hint,
    string? TargetDirectory = null);
