using FieldOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.ContactEmail).HasMaxLength(200);
        b.Property(x => x.ContactPhone).HasMaxLength(50);
        b.Property(x => x.MikroServer).HasMaxLength(200);
        b.Property(x => x.MikroDatabase).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.HasMany(x => x.ApiKeys)
            .WithOne(x => x.Tenant!)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenantApiKeyConfiguration : IEntityTypeConfiguration<TenantApiKey>
{
    public void Configure(EntityTypeBuilder<TenantApiKey> b)
    {
        b.ToTable("tenant_api_keys");
        b.HasKey(x => x.Id);
        b.Property(x => x.KeyHash).HasMaxLength(64).IsRequired();   // SHA-256 hex
        b.Property(x => x.KeyPrefix).HasMaxLength(20).IsRequired();
        b.Property(x => x.Label).HasMaxLength(200);
        b.Property(x => x.AgentId).HasMaxLength(200);
        b.Property(x => x.Scope).HasConversion<int>();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedBy).HasMaxLength(200);
        b.HasIndex(x => x.KeyHash).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}

public class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> b)
    {
        b.ToTable("sync_runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Direction).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.TableName).HasMaxLength(200);
        b.Property(x => x.AgentId).HasMaxLength(200);
        b.Property(x => x.BatchId).HasMaxLength(100);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        b.Property(x => x.ErrorCategory).HasConversion<int?>();
        b.Property(x => x.CheckpointFrom).HasMaxLength(200);
        b.Property(x => x.CheckpointTo).HasMaxLength(200);
        b.HasIndex(x => new { x.TenantId, x.StartedAt });
        b.HasIndex(x => new { x.TenantId, x.TableName, x.StartedAt });
        b.HasIndex(x => x.BatchId);
    }
}

public class OutboxItemConfiguration : IEntityTypeConfiguration<OutboxItem>
{
    public void Configure(EntityTypeBuilder<OutboxItem> b)
    {
        b.ToTable("outbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.DeviceId).HasMaxLength(200);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.ErpRef).HasMaxLength(200);
        b.Property(x => x.LockedByAgentId).HasMaxLength(200);
        // Aynı tenant + aynı idempotency_key ile sadece 1 kayıt
        b.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }
}

public class SyncStateConfiguration : IEntityTypeConfiguration<SyncState>
{
    public void Configure(EntityTypeBuilder<SyncState> b)
    {
        b.ToTable("sync_state");
        b.HasKey(x => x.Id);
        b.Property(x => x.TableName).HasMaxLength(200).IsRequired();
        b.Property(x => x.TableSchema).HasMaxLength(50);
        b.Property(x => x.LastStatus).HasConversion<int>();
        b.Property(x => x.CheckpointRv).HasMaxLength(100);
        b.Property(x => x.LastError).HasMaxLength(2000);
        // Her tenant + tablo için tek satır
        b.HasIndex(x => new { x.TenantId, x.TableName }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.DeadLettered });
    }
}

public class AndroidDeviceConfiguration : IEntityTypeConfiguration<AndroidDevice>
{
    public void Configure(EntityTypeBuilder<AndroidDevice> b)
    {
        b.ToTable("android_devices");
        b.HasKey(x => x.Id);
        b.Property(x => x.DeviceId).HasMaxLength(200).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Model).HasMaxLength(200);
        b.Property(x => x.OsVersion).HasMaxLength(50);
        b.Property(x => x.AppVersion).HasMaxLength(50);
        b.Property(x => x.ApiKeyId).HasMaxLength(100);
        b.HasIndex(x => new { x.TenantId, x.DeviceId }).IsUnique();
    }
}

public class AgentEventConfiguration : IEntityTypeConfiguration<AgentEvent>
{
    public void Configure(EntityTypeBuilder<AgentEvent> b)
    {
        b.ToTable("agent_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.AgentId).HasMaxLength(200);
        b.Property(x => x.AgentVersion).HasMaxLength(50);
        b.Property(x => x.Level).HasConversion<int>();
        b.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        b.Property(x => x.Exception).HasColumnType("text");
        b.Property(x => x.Category).HasMaxLength(50);
        b.Property(x => x.ContextJson).HasColumnType("jsonb");
        b.Property(x => x.TableName).HasMaxLength(200);
        b.HasIndex(x => new { x.TenantId, x.OccurredAt });
        b.HasIndex(x => new { x.TenantId, x.Level, x.OccurredAt });
    }
}
