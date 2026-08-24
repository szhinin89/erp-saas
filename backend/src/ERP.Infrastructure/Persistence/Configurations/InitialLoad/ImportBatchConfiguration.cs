using ERP.Domain.Modules.InitialLoad.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.InitialLoad;

public sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("import_batches");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.ImportType).HasColumnName("import_type").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.Label).HasColumnName("label").HasMaxLength(200);

        builder.Property(x => x.TotalRows).HasColumnName("total_rows").IsRequired();
        builder.Property(x => x.ValidRows).HasColumnName("valid_rows").IsRequired();
        builder.Property(x => x.IssueRows).HasColumnName("issue_rows").IsRequired();
        builder.Property(x => x.WarningRows).HasColumnName("warning_rows").IsRequired();
        builder.Property(x => x.ImportedRows).HasColumnName("imported_rows").IsRequired();

        builder.Property(x => x.ValidatedAt).HasColumnName("validated_at");
        builder.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(1000);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasMany(x => x.Files)
            .WithOne()
            .HasForeignKey(nameof(ImportBatchFile.ImportBatchId))
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_import_batches_company");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.ImportType, x.Status })
            .HasDatabaseName("ix_import_batches_company_type_status");
    }
}
