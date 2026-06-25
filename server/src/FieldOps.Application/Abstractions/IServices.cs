using FieldOps.Application.Common;
using FieldOps.Application.Auth;
using FieldOps.Contracts.Common;
using FieldOps.Contracts.Auth;
using FieldOps.Contracts.Sync;
using FieldOps.Contracts.Outbox;
using FieldOps.Contracts.Events;

namespace FieldOps.Application.Abstractions;

/// <summary>
/// Tüm application servisleri için sözleşme. Somut implementasyonlar Infrastructure'da.
/// </summary>
public interface ITenantService
{
    Task<ServiceResult<Contracts.Auth.TenantResponse>> CreateAsync(TenantCreateRequest req, CancellationToken ct);
    Task<ServiceResult<Contracts.Auth.TenantResponse>> UpdateAsync(Guid id, TenantUpdateRequest req, CancellationToken ct);
    Task<ServiceResult<Contracts.Auth.TenantResponse>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ServiceResult<PagedResult<Contracts.Auth.TenantResponse>>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct);
}

public interface IApiKeyService
{
    Task<ServiceResult<ApiKeyCreatedResponse>> CreateAsync(Guid tenantId, ApiKeyCreateRequest req, string? createdBy, CancellationToken ct);
    Task<ServiceResult<List<ApiKeyResponse>>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<ServiceResult> RevokeAsync(Guid tenantId, Guid keyId, CancellationToken ct);
    Task<ServiceResult<TenantContext>> ValidateAsync(string apiKeyPlain, CancellationToken ct);
    Task TouchAsync(Guid apiKeyId, CancellationToken ct);
}

public interface ISyncService
{
    /// <summary>Ajan bir batch gönderdiğinde çağrılır.</summary>
    Task<ServiceResult<SyncPushResponse>> PushBatchAsync(TenantContext ctx, SyncPushRequest req, CancellationToken ct);

    /// <summary>Tüm tabloların sync durumunu döner.</summary>
    Task<ServiceResult<List<SyncStateResponse>>> GetStateAsync(TenantContext ctx, CancellationToken ct);

    /// <summary>Son sync_run'ları döner (admin UI için).</summary>
    Task<ServiceResult<PagedResult<SyncRunResponse>>> GetRunsAsync(TenantContext ctx, int page, int pageSize, CancellationToken ct);
}

public interface IOutboxService
{
    /// <summary>Android'den gelen item'ları kabul eder (idempotent).</summary>
    Task<ServiceResult<OutboxPushResponse>> PushAsync(TenantContext ctx, OutboxPushRequest req, CancellationToken ct);

    /// <summary>Windows ajanı pending item'ları çeker ve kilitler.</summary>
    Task<ServiceResult<OutboxPullResponse>> PullAsync(TenantContext ctx, OutboxPullRequest req, CancellationToken ct);

    /// <summary>Ajan, ERP yazma sonucunu bildirir.</summary>
    Task<ServiceResult> AckAsync(TenantContext ctx, OutboxAckRequest req, CancellationToken ct);
}

public interface IEventService
{
    /// <summary>Windows ajanından gelen log event'lerini toplu yazar.</summary>
    Task<ServiceResult> IngestAsync(TenantContext ctx, AgentEventBatch batch, CancellationToken ct);
}
