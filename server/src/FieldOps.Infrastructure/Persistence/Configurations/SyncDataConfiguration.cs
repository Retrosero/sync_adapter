using FieldOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

public class SyncDataConfiguration : IEntityTypeConfiguration<SyncData>
{
    public void Configure(EntityTypeBuilder<SyncData> b)
    {
        b.ToTable("sync_data");
        b.HasKey(x => x.Id);
        b.Property(x => x.TableName).HasMaxLength(200).IsRequired();
        b.Property(x => x.SourcePk).HasMaxLength(200).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.SyncBatchId).HasMaxLength(100);
        // Bir tenant + tablo + PK için tek satır (upsert için)
        b.HasIndex(x => new { x.TenantId, x.TableName, x.SourcePk }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.TableName, x.SyncedAt });
    }
}
