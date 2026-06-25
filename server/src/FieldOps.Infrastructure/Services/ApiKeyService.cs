using FieldOps.Application.Abstractions;
using FieldOps.Application.Auth;
using FieldOps.Application.Common;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.Infrastructure.Persistence;
using FieldOps.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using FieldOps.Contracts.Auth;

namespace FieldOps.Infrastructure.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly FieldOpsDbContext _db;
    private readonly TimeProvider _clock;

    public ApiKeyService(FieldOpsDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ServiceResult<ApiKeyCreatedResponse>> CreateAsync(
        Guid tenantId, ApiKeyCreateRequest req, string? createdBy, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FindAsync(new object?[] { tenantId }, ct);
        if (tenant is null) return ServiceResult<ApiKeyCreatedResponse>.Failure("TENANT_NOT_FOUND", "Tenant bulunamadı.");

        if (!Enum.TryParse<ApiKeyScope>(req.Scope, ignoreCase: true, out var scope))
            return ServiceResult<ApiKeyCreatedResponse>.Failure("INVALID_SCOPE", $"Geçersiz scope: {req.Scope}");

        var plain = ApiKeyGenerator.GeneratePlain(out var prefix);
        var hash = ApiKeyGenerator.Hash(plain);

        var key = new TenantApiKey
        {
            TenantId = tenantId,
            KeyHash = hash,
            KeyPrefix = prefix,
            Label = req.Label,
            AgentId = req.AgentId,
            Scope = scope,
            ExpiresAt = req.ExpiresAt,
            CreatedBy = createdBy,
            IsActive = true,
        };
        _db.TenantApiKeys.Add(key);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ApiKeyCreatedResponse>.Success(new ApiKeyCreatedResponse
        {
            Id = key.Id,
            TenantId = key.TenantId,
            PlainKey = plain,
            KeyPrefix = key.KeyPrefix,
            Label = key.Label,
            CreatedAt = key.CreatedAt,
            ExpiresAt = key.ExpiresAt,
        });
    }

    public async Task<ServiceResult<List<ApiKeyResponse>>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        var items = await _db.TenantApiKeys
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ApiKeyResponse
            {
                Id = x.Id,
                TenantId = x.TenantId,
                KeyPrefix = x.KeyPrefix,
                Label = x.Label,
                AgentId = x.AgentId,
                Scope = x.Scope.ToString(),
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                LastUsedAt = x.LastUsedAt,
                ExpiresAt = x.ExpiresAt,
                RevokedAt = x.RevokedAt,
            })
            .ToListAsync(ct);
        return ServiceResult<List<ApiKeyResponse>>.Success(items);
    }

    public async Task<ServiceResult> RevokeAsync(Guid tenantId, Guid keyId, CancellationToken ct)
    {
        var key = await _db.TenantApiKeys
            .FirstOrDefaultAsync(x => x.Id == keyId && x.TenantId == tenantId, ct);
        if (key is null) return ServiceResult.Failure("KEY_NOT_FOUND", "API key bulunamadı.");
        if (key.RevokedAt is not null) return ServiceResult.Failure("ALREADY_REVOKED", "API key zaten revoke edilmiş.");
        key.IsActive = false;
        key.RevokedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<TenantContext>> ValidateAsync(string apiKeyPlain, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKeyPlain))
            return ServiceResult<TenantContext>.Failure("MISSING_KEY", "API key boş olamaz.");

        var hash = ApiKeyGenerator.Hash(apiKeyPlain);
        var key = await _db.TenantApiKeys
            .Include(x => x.Tenant)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.KeyHash == hash, ct);

        if (key is null) return ServiceResult<TenantContext>.Failure("INVALID_KEY", "API key geçersiz.");
        if (!key.IsActive || key.RevokedAt is not null)
            return ServiceResult<TenantContext>.Failure("KEY_DISABLED", "API key devre dışı.");
        if (key.ExpiresAt is not null && key.ExpiresAt < _clock.GetUtcNow().UtcDateTime)
            return ServiceResult<TenantContext>.Failure("KEY_EXPIRED", "API key süresi dolmuş.");
        if (key.Tenant is null || !key.Tenant.IsActive)
            return ServiceResult<TenantContext>.Failure("TENANT_DISABLED", "Tenant pasif durumda.");

        return ServiceResult<TenantContext>.Success(new TenantContext
        {
            TenantId = key.TenantId,
            ApiKeyId = key.Id,
            AgentId = key.AgentId,
            Scope = key.Scope,
            Label = key.Label,
        });
    }

    public async Task TouchAsync(Guid apiKeyId, CancellationToken ct)
    {
        // Best-effort: hata olursa yut. last_used_at'ın senkron tutulması kritik değil.
        try
        {
            var key = await _db.TenantApiKeys.FirstOrDefaultAsync(x => x.Id == apiKeyId, ct);
            if (key is null) return;
            key.LastUsedAt = _clock.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Loglama middleware'de yapılacak.
        }
    }
}
