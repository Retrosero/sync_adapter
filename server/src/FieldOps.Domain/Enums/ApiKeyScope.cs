namespace FieldOps.Domain.Enums;

/// <summary>
/// API anahtarının kapsamı. Tenant scope zorunlu multi-tenant izolasyon uygular.
/// Admin scope sadece süper admin UI'ı için, RLS'yi bypass eder.
/// </summary>
public enum ApiKeyScope
{
    Tenant = 1,     // Normal tenant API key — RLS aktif
    Admin = 2       // Super admin — RLS bypass (sadece admin UI)
}
