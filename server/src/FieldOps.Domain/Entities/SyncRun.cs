using FieldOps.Domain.Enums;

namespace FieldOps.Domain.Entities;

/// <summary>
/// Tek bir senkronizasyon oturumunun kaydı. ERP→Sunucu veya Sunucu→ERP her çalıştığında bir satır oluşur.
/// Admin UI'da "son sync ne zaman, kaç satır, hata var mı" sorularını cevaplar.
/// </summary>
public class SyncRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public SyncDirection Direction { get; set; }
    public string? TableName { get; set; }                  // null ise tüm tablolar (orchestrator run)
    public string? AgentId { get; set; }                    // Hangi Windows ajanı tetikledi
    public string? BatchId { get; set; }                   // Hangi batch'le ilişkili
    public SyncStatus Status { get; set; } = SyncStatus.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public long? DurationMs { get; set; }
    public long RowsTotal { get; set; }
    public long RowsSynced { get; set; }
    public long RowsFailed { get; set; }
    public string? ErrorMessage { get; set; }
    public ErrorCategory? ErrorCategory { get; set; }
    public string? CheckpointFrom { get; set; }             // JSON: önceki checkpoint
    public string? CheckpointTo { get; set; }               // JSON: yeni checkpoint

    // Navigation
    public Tenant? Tenant { get; set; }
}
