namespace FieldOps.Domain.Entities;

/// <summary>
/// Generic sync payload. ERP'den gelen her satır burada jsonb olarak saklanır.
/// F4'te specific entity'lere geçirilecek (CariHesap, Stok, vb.).
/// Şimdilik MVP için tek tablo yeterli, sorgulama performansı düşük ama çalışır.
/// </summary>
public class SyncData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string TableName { get; set; } = string.Empty;       // MikroDB'deki tablo adı (küçük harf)
    public string SourcePk { get; set; } = string.Empty;       // Primary key value
    public string PayloadJson { get; set; } = "{}";            // Tüm kolonlar
    public DateTime? SourceModifiedAt { get; set; }            // Mikro'daki last_modified
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public string? SyncBatchId { get; set; }
}
