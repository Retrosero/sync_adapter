using FieldOps.Domain.Enums;

namespace FieldOps.Domain.Entities;

/// <summary>
/// Windows ajanlarından sunucuya push'lanan log event'leri.
/// Admin UI'da ajanın durumunu canlı görmek için kullanılır.
/// Serilog custom HTTP sink bu tabloya yazar.
/// </summary>
public class AgentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string? AgentId { get; set; }                   // Ajan makine fingerprint
    public string? AgentVersion { get; set; }
    public EventLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? Category { get; set; }                  // "sync", "auth", "config", "system"
    public string? ContextJson { get; set; }               // Ek structured alanlar
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? TableName { get; set; }                 // Hangi tablo ile ilgili (varsa)
    public Guid? RunId { get; set; }                       // İlgili sync_run (varsa)
}
