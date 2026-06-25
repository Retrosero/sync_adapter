using FieldOps.Domain.Enums;

namespace FieldOps.Domain.Entities;

/// <summary>
/// Android'den gelen ve henüz ERP'ye yazılmamış belgelerin kuyruğu.
/// idempotency_key UNIQUE — aynı anahtarla gelen tekrar istekler no-op olarak işlenir.
/// Bu sayede offline-first Android + güvenilir olmayan ağ koşullarında kayıp olmaz.
/// </summary>
public class OutboxItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;  // UNIQUE per tenant
    public string DocumentType { get; set; } = string.Empty;    // "sales_order", "collection" vb.
    public string PayloadJson { get; set; } = string.Empty;     // Orijinal payload
    public string? DeviceId { get; set; }                       // Hangi Android cihazdan geldi
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DispatchedAt { get; set; }                 // Ajan aldığı an
    public DateTime? AckedAt { get; set; }                      // ERP'ye yazılıp ACK geldiği an
    public int RetryCount { get; set; } = 0;
    public string? LastError { get; set; }
    public string? ErpRef { get; set; }                         // ERP'den dönen referans (evrak no vb.)
    public string? LockedByAgentId { get; set; }                // Hangi ajan şu an işliyor
    public DateTime? LockedAt { get; set; }
    public DateTime? LockExpiresAt { get; set; }                // Lock süresi dolunca başka ajan alabilir
}
