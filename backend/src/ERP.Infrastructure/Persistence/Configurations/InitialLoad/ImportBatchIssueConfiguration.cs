using ERP.Domain.Modules.InitialLoad.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.InitialLoad;

public sealed class ImportBatchIssueConfiguration : IEntityTypeConfiguration<ImportBatchIssue>
{
    public void Configure(EntityTypeBuilder<ImportBatchIssue> builder)
    {
        builder.ToTable("import_batch_issues");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ImportBatchId).HasColumnName("import_batch_id").IsRequired();
        builder.Property(x => x.ImportBatchRowId).HasColumnName("import_batch_row_id").IsRequired();

        builder.Property(x => x.RowNumber).HasColumnName("row_number").IsRequired();
        builder
            .Property(x => x.FieldName)
            .HasColumnName("field_name")
            .HasMaxLength(ImportBatchIssue.FieldNameMaxLen);
        builder.Property(x => x.Severity).HasColumnName("severity").IsRequired();
        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(ImportBatchIssue.CodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Message)
            .HasColumnName("message")
            .HasMaxLength(ImportBatchIssue.MessageMaxLen)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => x.ImportBatchRowId)
            .HasDatabaseName("ix_import_batch_issues_row");

        builder
            .HasIndex(x => new { x.ImportBatchId, x.Severity })
            .HasDatabaseName("ix_import_batch_issues_batch_severity");
    }
}
