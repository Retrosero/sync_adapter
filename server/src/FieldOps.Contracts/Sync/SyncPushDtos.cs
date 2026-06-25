namespace FieldOps.Contracts.Sync;

/// <summary>
/// Windows ajanının ERP'den çekip sunucuya gönderdiği tek bir batch.
/// </summary>
public class SyncPushRequest
{
    public Guid TenantId { get; set; }
    public string AgentId { get; set; } = string.Empty;        // Makine fingerprint
    public string TableName { get; set; } = string.Empty;     // MikroDB'deki tablo
    public string BatchId { get; set; } = string.Empty;        // Unique batch id
    public bool IsInitial { get; set; }                        // true = full pull, false = delta
    public DateTime? CheckpointFrom { get; set; }              // Önceki checkpoint
    public DateTime? CheckpointTo { get; set; }                // Bu batch'in son checkpoint'i
    public List<SyncRow> Rows { get; set; } = new();
}

/// <summary>
/// Tek bir satır. Kolonlar dictionary olarak taşınır çünkü tablo şeması runtime'da keşfedilir.
/// JSON deserialization esnasında System.Text.Json dictionary key'leri string olarak işler.
/// </summary>
public class SyncRow
{
    /// <summary>
    /// MikroDB'deki primary key değeri (string representation). Server bunu kullanarak upsert yapar.
    /// Genelde tek kolon PK; composite ise "col1|col2" formatında birleştirilir.
    /// </summary>
    public string PrimaryKey { get; set; } = string.Empty;

    public Dictionary<string, object?> Columns { get; set; } = new();
}

public class SyncPushResponse
{
    public string BatchId { get; set; } = string.Empty;
    public int RowsAccepted { get; set; }
    public int RowsRejected { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime? NextCheckpoint { get; set; }
}
