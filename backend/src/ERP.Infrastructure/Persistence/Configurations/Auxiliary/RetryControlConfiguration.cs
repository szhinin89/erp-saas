using ERP.Domain.Modules.Auxiliary.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Auxiliary;

public class RetryControlConfiguration : IEntityTypeConfiguration<RetryControl>
{
    public void Configure(EntityTypeBuilder<RetryControl> builder)
    {
        builder.ToTable("retry_control");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.DocId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.RetryCount).HasColumnName("retry_count").HasDefaultValue((short)0);
        builder.Property(x => x.MaxRetries).HasColumnName("max_retries").HasDefaultValue((short)5);
        builder.Property(x => x.LastAttemptAt).HasColumnName("last_attempt_at");
        builder.Property(x => x.NextRetryAt).HasColumnName("next_retry_at");
        builder.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(1000);
        builder.Property(x => x.IsExhausted).HasColumnName("is_exhausted").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

        // Un solo registro de retry por documento
        builder.HasIndex(x => x.DocId).IsUnique().HasDatabaseName("uq_retry_doc");
        // Índice para el job de reintentos automáticos
        builder.HasIndex(x => new { x.CompanyId, x.NextRetryAt })
            .HasFilter("is_exhausted = false")
            .HasDatabaseName("idx_retry_next");
    }
}
