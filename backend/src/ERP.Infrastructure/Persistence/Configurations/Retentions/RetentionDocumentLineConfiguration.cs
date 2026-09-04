using ERP.Domain.Modules.Retentions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Retentions;

/// <summary>
/// Fase <c>RETENTIONS-PERSISTENCE-01B</c>. Línea hija de <see cref="RetentionDocument"/> — mismo
/// patrón que <c>ExpenseLineConfiguration</c>.
/// </summary>
public sealed class RetentionDocumentLineConfiguration : IEntityTypeConfiguration<RetentionDocumentLine>
{
    public void Configure(EntityTypeBuilder<RetentionDocumentLine> builder)
    {
        builder.ToTable("retention_document_lines");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder
            .Property(x => x.RetentionDocumentId)
            .HasColumnName("retention_document_id")
            .IsRequired();
        builder.Property(x => x.TaxType).HasColumnName("tax_type").HasConversion<int>().IsRequired();
        builder
            .Property(x => x.RetentionCode)
            .HasColumnName("retention_code")
            .HasMaxLength(RetentionDocumentLine.RetentionCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.BaseAmount)
            .HasColumnName("base_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.RetentionRate)
            .HasColumnName("retention_rate")
            .HasColumnType("numeric(5,2)")
            .IsRequired();
        builder
            .Property(x => x.RetainedAmount)
            .HasColumnName("retained_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(RetentionDocumentLine.DescriptionMaxLen);

        builder
            .HasIndex(x => x.RetentionDocumentId)
            .HasDatabaseName("ix_retention_document_lines_document");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_retention_document_lines_tenant");
    }
}
