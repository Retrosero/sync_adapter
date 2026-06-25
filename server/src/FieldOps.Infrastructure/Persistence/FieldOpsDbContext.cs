using FieldOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Infrastructure.Persistence;

public class FieldOpsDbContext : DbContext
{
    public FieldOpsDbContext(DbContextOptions<FieldOpsDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantApiKey> TenantApiKeys => Set<TenantApiKey>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<OutboxItem> OutboxItems => Set<OutboxItem>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<AndroidDevice> AndroidDevices => Set<AndroidDevice>();
    public DbSet<AgentEvent> AgentEvents => Set<AgentEvent>();
    public DbSet<SyncData> SyncData => Set<SyncData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("fieldops");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FieldOpsDbContext).Assembly);
    }
}
