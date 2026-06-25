namespace FieldOps.Domain.Entities;

/// <summary>
/// Bir şirketi (tenant) temsil eder. Tüm iş verisi bu id üzerinden izole edilir.
/// PostgreSQL'de Row-Level Security policy'si ile korunur.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;          // Kısa kod (admin UI'da görünür)
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? MikroServer { get; set; }                  // ERP sunucu (örn: GURBUZ)
    public string? MikroDatabase { get; set; }               // ERP veritabanı
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    // Navigation
    public List<TenantApiKey> ApiKeys { get; set; } = new();
}
