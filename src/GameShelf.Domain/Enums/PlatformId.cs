namespace GameShelf.Domain.Enums;

/// <summary>
/// Desteklenen (ve placeholder olarak duran) platformlar.
/// Değerler veritabanındaki <c>Platforms.Id</c> ile birebir aynıdır — değiştirmeyin.
/// </summary>
public enum PlatformId
{
    /// <summary>Tespit edilemedi; kullanıcı elle düzeltmelidir.</summary>
    Unknown = 0,

    Ps1 = 1,
    Ps2 = 2,
    Ps3 = 3,

    /// <summary>Emülasyon sağlanmaz; yalnızca UI placeholder'ı.</summary>
    Ps4 = 4,

    /// <summary>Emülasyon sağlanmaz; yalnızca UI placeholder'ı.</summary>
    Ps5 = 5
}
