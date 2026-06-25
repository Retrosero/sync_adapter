using FieldOps.Domain.Enums;

namespace FieldOps.Domain.Entities;

/// <summary>
/// Her (tenant, tablo) çifti için delta sync durumunu tutar.
/// Windows ajanı başlarken bu tabloya danışarak nereden devam edeceğini bilir.
/// </summary>
public class SyncState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string TableName { get; set; } = string.Empty;   // MikroDB'deki tablo adı
    public string? TableSchema { get; set; }                // "dbo" (ileride çoklu şema için)
    public DateTime? LastRunAt { get; set; }
    public SyncStatus LastStatus { get; set; } = SyncStatus.Pending;
    public long RowsTotalSynced { get; set; }
    public long RowsInLastRun { get; set; }
    public DateTime? CheckpointTs { get; set; }             // Son senkronize edilen last_modified
    public string? CheckpointRv { get; set; }               // ROWVERSION binary (hex string)
    public bool IsInitial { get; set; } = true;             // true ise sonraki sync full pull
    public int RetryCount { get; set; } = 0;
    public bool DeadLettered { get; set; } = false;
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
