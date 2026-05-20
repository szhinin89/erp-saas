using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchaseDetailConfiguration : IEntityTypeConfiguration<PurchaseDetail>
{
    public void Configure(EntityTypeBuilder<PurchaseDetail> builder)
    {
        builder.ToTable("purchase_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.PurchaseDocumentId).HasColumnName("purchase_document_id").IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("product_id");
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(18, 4);
        builder.Property(e => e.QtyReceived).HasColumnName("qty_received").HasPrecision(18, 4);
        builder.Property(e => e.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4);
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4);
        builder.Property(e => e.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 4);
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4);

        builder.HasIndex(e => e.PurchaseDocumentId).HasDatabaseName("ix_purchase_detail_document");
    }
}
