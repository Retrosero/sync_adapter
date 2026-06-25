namespace FieldOps.Domain.Common;

/// <summary>
/// Tüm entity'ler için ortak alanlar. İleride audit trail için kullanılabilir.
/// Şimdilik her entity'de manuel tanımlı, IEntityBase'i uygulamak opsiyonel bırakıldı.
/// </summary>
public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
