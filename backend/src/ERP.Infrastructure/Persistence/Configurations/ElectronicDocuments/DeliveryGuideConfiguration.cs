using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class DeliveryGuideConfiguration : IEntityTypeConfiguration<DeliveryGuide>
{
    public void Configure(EntityTypeBuilder<DeliveryGuide> builder)
    {
        builder.ToTable("delivery_guide");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.SalesInvoiceId).HasColumnName("sales_invoice_id");
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.CarrierRuc).HasColumnName("carrier_ruc").HasMaxLength(13).IsFixedLength();
        builder.Property(x => x.CarrierName).HasColumnName("carrier_name").HasMaxLength(200);
        builder.Property(x => x.CarrierPlate).HasColumnName("carrier_plate").HasMaxLength(20);
        builder.Property(x => x.Route).HasColumnName("route").HasMaxLength(300);
        builder.Property(x => x.DestIdType).HasColumnName("dest_id_type").HasMaxLength(5);
        builder.Property(x => x.DestIdNumber).HasColumnName("dest_id_number").HasMaxLength(32);
        builder.Property(x => x.DestName).HasColumnName("dest_name").HasMaxLength(200);
        builder.Property(x => x.DestAddress).HasColumnName("dest_address").HasMaxLength(500).IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id");

        builder.HasOne(x => x.SalesInvoiceDoc)
            .WithMany()
            .HasForeignKey(x => x.SalesInvoiceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
