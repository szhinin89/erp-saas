using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchaseElectronicDocConfiguration : IEntityTypeConfiguration<PurchaseElectronicDoc>
{
    public void Configure(EntityTypeBuilder<PurchaseElectronicDoc> builder)
    {
        builder.ToTable("purchase_electronic_doc");

        builder.HasKey(e => e.PurchaseDocumentId);
        builder.Property(e => e.PurchaseDocumentId).HasColumnName("purchase_document_id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(49);
        builder.Property(e => e.XmlPath).HasColumnName("xml_path").HasMaxLength(500);
    }
}
