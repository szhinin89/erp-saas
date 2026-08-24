using ERP.Domain.Modules.InitialLoad.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.InitialLoad;

public sealed class ImportBatchFileConfiguration : IEntityTypeConfiguration<ImportBatchFile>
{
    public void Configure(EntityTypeBuilder<ImportBatchFile> builder)
    {
        builder.ToTable("import_batch_files");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ImportBatchId).HasColumnName("import_batch_id").IsRequired();

        builder
            .Property(x => x.StoredPath)
            .HasColumnName("stored_path")
            .HasMaxLength(ImportBatchFile.StoredPathMaxLen)
            .IsRequired();
        builder
            .Property(x => x.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(ImportBatchFile.FileNameMaxLen)
            .IsRequired();
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(x => x.UploadedAt).HasColumnName("uploaded_at").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasIndex(x => x.ImportBatchId)
            .HasDatabaseName("ix_import_batch_files_batch");
    }
}
