using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("sales_invoice");
        // PK = FK a electronic_doc.id (shared primary key association — sin default)
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BuyerIdType).HasColumnName("buyer_id_type").HasMaxLength(5).IsRequired();
        builder.Property(x => x.BuyerIdNumber).HasColumnName("buyer_id_number").HasMaxLength(32).IsRequired();
        builder.Property(x => x.BuyerName).HasColumnName("buyer_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.BuyerAddress).HasColumnName("buyer_address").HasMaxLength(500);
        builder.Property(x => x.BuyerEmail).HasColumnName("buyer_email").HasMaxLength(120);
        builder.Property(x => x.BuyerPhone).HasColumnName("buyer_phone").HasMaxLength(40);
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(x => x.SalespersonId).HasColumnName("salesperson_id");
        builder.Property(x => x.DeliveryDate).HasColumnName("delivery_date");
        builder.Property(x => x.RemittanceKey).HasColumnName("remittance_key").HasMaxLength(49);
        builder.Property(x => x.GuideDocNum).HasColumnName("guide_doc_num").HasMaxLength(20);

        builder.HasIndex(x => x.CompanyId).HasDatabaseName("idx_si_company");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("idx_si_customer");
        builder.HasIndex(x => new { x.CompanyId, x.BuyerIdNumber }).HasDatabaseName("idx_si_buyer_id");

        builder.HasOne(x => x.BuyerIdTypeNav)
            .WithMany().HasForeignKey(x => x.BuyerIdType).OnDelete(DeleteBehavior.Restrict);
        // La relación 1:1 con ElectronicDoc se configura en ElectronicDocConfiguration
    }
}
