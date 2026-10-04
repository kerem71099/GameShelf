namespace GameShelf.Domain.Enums;

/// <summary>Başlatma öncesi kontrol bulgusunun ağırlığı.</summary>
public enum LaunchIssueSeverity
{
    /// <summary>Bilgilendirme; başlatmayı etkilemez.</summary>
    Info = 0,

    /// <summary>Uyarı; kullanıcı onaylarsa başlatma devam eder.</summary>
    Warning = 1,

    /// <summary>Hata; başlatma engellenir.</summary>
    Error = 2
}
