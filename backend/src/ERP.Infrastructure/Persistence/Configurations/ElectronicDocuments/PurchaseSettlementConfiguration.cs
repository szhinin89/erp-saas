using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class PurchaseSettlementConfiguration : IEntityTypeConfiguration<PurchaseSettlement>
{
    public void Configure(EntityTypeBuilder<PurchaseSettlement> builder)
    {
        builder.ToTable("purchase_settlement");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.SellerIdType).HasColumnName("seller_id_type").HasMaxLength(5);
        builder.Property(x => x.SellerIdNum).HasColumnName("seller_id_num").HasMaxLength(32).IsRequired();
        builder.Property(x => x.SellerName).HasColumnName("seller_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SellerAddress).HasColumnName("seller_address").HasMaxLength(300);
        builder.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
    }
}
