using FieldOps.Domain.Enums;

namespace FieldOps.Application.Auth;

/// <summary>
/// Middleware tarafından HttpContext.Items'a konan tenant bilgisi.
/// Tüm DB sorgularında bu tenant üzerinden RLS uygulanır.
/// </summary>
public class TenantContext
{
    public Guid TenantId { get; init; }
    public Guid ApiKeyId { get; init; }
    public string? AgentId { get; init; }
    public ApiKeyScope Scope { get; init; }
    public string? Label { get; init; }
    public bool BypassRls => Scope == ApiKeyScope.Admin;
}
