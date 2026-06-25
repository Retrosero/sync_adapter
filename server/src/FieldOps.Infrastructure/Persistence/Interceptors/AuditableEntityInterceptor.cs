using FieldOps.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FieldOps.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Insert/Update sırasında CreatedAt/UpdatedAt alanlarını otomatik set eder.
/// CreatedBy/UpdatedBy şimdilik null — ileride HttpContext'ten alınabilir.
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private static void ApplyAudit(DbContext? ctx)
    {
        if (ctx is null) return;
        var now = DateTime.UtcNow;
        foreach (EntityEntry entry in ctx.ChangeTracker.Entries())
        {
            if (entry.Entity is not AuditableEntity entity) continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    entity.CreatedAt = now;
                    entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entity.UpdatedAt = now;
                    // CreatedAt değiştirilmesin
                    entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                    break;
            }
        }
    }
}
