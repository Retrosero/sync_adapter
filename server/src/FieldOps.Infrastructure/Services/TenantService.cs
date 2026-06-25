using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Contracts.Common;
using FieldOps.Domain.Entities;
using FieldOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FieldOps.Contracts.Auth;

namespace FieldOps.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly FieldOpsDbContext _db;

    public TenantService(FieldOpsDbContext db) => _db = db;

    public async Task<ServiceResult<TenantResponse>> CreateAsync(TenantCreateRequest req, CancellationToken ct)
    {
        // Validation
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Name)) errors[nameof(req.Name)] = new[] { "Ad zorunlu." };
        if (string.IsNullOrWhiteSpace(req.Code)) errors[nameof(req.Code)] = new[] { "Kod zorunlu." };
        if (errors.Count > 0)
            return ServiceResult<TenantResponse>.Failure("VALIDATION_ERROR", "Validasyon hatası.", errors);

        // Unique code check
        if (await _db.Tenants.AnyAsync(x => x.Code == req.Code, ct))
            return ServiceResult<TenantResponse>.Failure("CODE_TAKEN", "Bu kod zaten kullanımda.");

        var tenant = new Tenant
        {
            Name = req.Name.Trim(),
            Code = req.Code.Trim().ToUpperInvariant(),
            ContactEmail = req.ContactEmail?.Trim(),
            ContactPhone = req.ContactPhone?.Trim(),
            MikroServer = req.MikroServer?.Trim(),
            MikroDatabase = req.MikroDatabase?.Trim(),
            Notes = req.Notes,
            IsActive = true,
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<TenantResponse>.Success(Map(tenant));
    }

    public async Task<ServiceResult<TenantResponse>> UpdateAsync(Guid id, TenantUpdateRequest req, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FindAsync(new object?[] { id }, ct);
        if (tenant is null) return ServiceResult<TenantResponse>.Failure("TENANT_NOT_FOUND", "Tenant bulunamadı.");

        if (req.Name is not null) tenant.Name = req.Name.Trim();
        if (req.ContactEmail is not null) tenant.ContactEmail = req.ContactEmail.Trim();
        if (req.ContactPhone is not null) tenant.ContactPhone = req.ContactPhone.Trim();
        if (req.MikroServer is not null) tenant.MikroServer = req.MikroServer.Trim();
        if (req.MikroDatabase is not null) tenant.MikroDatabase = req.MikroDatabase.Trim();
        if (req.Notes is not null) tenant.Notes = req.Notes;
        if (req.IsActive.HasValue) tenant.IsActive = req.IsActive.Value;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ServiceResult<TenantResponse>.Success(Map(tenant));
    }

    public async Task<ServiceResult<TenantResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .Include(x => x.ApiKeys)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (tenant is null) return ServiceResult<TenantResponse>.Failure("TENANT_NOT_FOUND", "Tenant bulunamadı.");
        return ServiceResult<TenantResponse>.Success(Map(tenant));
    }

    public async Task<ServiceResult<PagedResult<TenantResponse>>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.Tenants.OrderBy(x => x.Name).AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TenantResponse
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                ContactEmail = x.ContactEmail,
                ContactPhone = x.ContactPhone,
                MikroServer = x.MikroServer,
                MikroDatabase = x.MikroDatabase,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                ApiKeyCount = x.ApiKeys.Count(a => a.IsActive && a.RevokedAt == null)
            })
            .ToListAsync(ct);
        return ServiceResult<PagedResult<TenantResponse>>.Success(
            new PagedResult<TenantResponse> { Items = items, Total = total, Page = page, PageSize = pageSize });
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FindAsync(new object?[] { id }, ct);
        if (tenant is null) return ServiceResult.Failure("TENANT_NOT_FOUND", "Tenant bulunamadı.");
        _db.Tenants.Remove(tenant);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Success();
    }

    private static TenantResponse Map(Tenant t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Code = t.Code,
        ContactEmail = t.ContactEmail,
        ContactPhone = t.ContactPhone,
        MikroServer = t.MikroServer,
        MikroDatabase = t.MikroDatabase,
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt,
        ApiKeyCount = t.ApiKeys?.Count(a => a.IsActive && a.RevokedAt == null) ?? 0,
    };
}
