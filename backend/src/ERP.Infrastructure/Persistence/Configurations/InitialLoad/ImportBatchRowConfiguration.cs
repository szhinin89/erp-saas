using ERP.Domain.Modules.InitialLoad.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.InitialLoad;

public sealed class ImportBatchRowConfiguration : IEntityTypeConfiguration<ImportBatchRow>
{
    public void Configure(EntityTypeBuilder<ImportBatchRow> builder)
    {
        builder.ToTable("import_batch_rows");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ImportBatchId).HasColumnName("import_batch_id").IsRequired();

        builder.Property(x => x.RowNumber).HasColumnName("row_number").IsRequired();
        builder.Property(x => x.RawData).HasColumnName("raw_data").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ParsedData).HasColumnName("parsed_data").HasColumnType("jsonb");
        builder.Property(x => x.HasBlockingIssue).HasColumnName("has_blocking_issue").IsRequired();
        builder.Property(x => x.IsImported).HasColumnName("is_imported").IsRequired();
        builder.Property(x => x.CreatedBusinessPartnerId).HasColumnName("created_business_partner_id");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => new { x.ImportBatchId, x.RowNumber })
            .IsUnique()
            .HasDatabaseName("ix_import_batch_rows_batch_row_number");

        builder
            .HasIndex(x => new { x.ImportBatchId, x.HasBlockingIssue })
            .HasDatabaseName("ix_import_batch_rows_batch_blocking");
    }
}
