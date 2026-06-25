using FieldOps.Domain.Enums;

namespace FieldOps.Domain.Entities;

/// <summary>
/// Tenant'ın API anahtarı. Saklanan değer SHA-256 hash'tir — düz metin asla DB'de tutulmaz.
/// KeyPrefix, admin UI'da "fo_live_a1b2c3..." gibi gösterim için kullanılır (ilk 12 char).
/// </summary>
public class TenantApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string KeyHash { get; set; } = string.Empty;       // SHA-256 hex (64 char)
    public string KeyPrefix { get; set; } = string.Empty;     // Görüntüleme için
    public string? Label { get; set; }                        // "windows-ajan-1", "android-tablo-7" gibi
    public string? AgentId { get; set; }                     // Hangi Windows ajanı / Android cihaz için
    public ApiKeyScope Scope { get; set; } = ApiKeyScope.Tenant;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? CreatedBy { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }
}
