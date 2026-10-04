using GameShelf.Domain.Enums;

namespace GameShelf.Application.Models;

/// <summary>
/// BIOS/firmware yolu kontrol sonucu.
/// GameShelf bu dosyaları SAĞLAMAZ; yalnızca kullanıcının verdiği yolun varlığını kontrol eder.
/// </summary>
public sealed record BiosStatus(
    PlatformId PlatformId,
    string PlatformName,
    bool Required,
    bool Configured,
    bool Exists,
    string? Path,
    string Hint);
