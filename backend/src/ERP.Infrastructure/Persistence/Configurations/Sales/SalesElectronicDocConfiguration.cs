using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesElectronicDocConfiguration : IEntityTypeConfiguration<SalesElectronicDoc>
{
    public void Configure(EntityTypeBuilder<SalesElectronicDoc> builder)
    {
        builder.ToTable("sales_electronic_doc");

        builder.HasKey(e => e.SalesDocumentId);
        builder.Property(e => e.SalesDocumentId).HasColumnName("sales_document_id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.EmissionPointId).HasColumnName("emission_point_id");
        builder.Property(e => e.LegacyElectronicDocId).HasColumnName("legacy_electronic_doc_id");
        builder.Property(e => e.DocTypeCode).HasColumnName("doc_type_code").HasMaxLength(2);
        builder.Property(e => e.AccessKey).HasColumnName("access_key").HasMaxLength(49);
        builder.Property(e => e.AuthNumber).HasColumnName("auth_number").HasMaxLength(49);
        builder.Property(e => e.AuthDate).HasColumnName("auth_date");
        builder.Property(e => e.XmlSignedPath).HasColumnName("xml_signed_path").HasMaxLength(500);
        builder.Property(e => e.XmlAuthPath).HasColumnName("xml_auth_path").HasMaxLength(500);
        builder.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(10);
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000);
        builder.Property(e => e.BuyerIdType).HasColumnName("buyer_id_type").HasMaxLength(5);
        builder.Property(e => e.BuyerIdNumber).HasColumnName("buyer_id_number").HasMaxLength(32);
        builder.Property(e => e.BuyerName).HasColumnName("buyer_name").HasMaxLength(200);
        builder.Property(e => e.BuyerAddress).HasColumnName("buyer_address").HasMaxLength(500);
        builder.Property(e => e.BuyerEmail).HasColumnName("buyer_email").HasMaxLength(120);
        builder.Property(e => e.BuyerPhone).HasColumnName("buyer_phone").HasMaxLength(40);

        builder.HasIndex(e => e.AccessKey)
            .IsUnique()
            .HasFilter("access_key IS NOT NULL")
            .HasDatabaseName("uq_sales_electronic_doc_access_key");
    }
}
