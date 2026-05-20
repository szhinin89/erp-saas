using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("purchase_order_line");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(d => d.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
        builder.Property(d => d.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(d => d.Description).HasColumnName("description").HasMaxLength(PurchaseOrderLine.DescriptionMaxLen).IsRequired();
        builder.Property(d => d.OrderedQty).HasColumnName("ordered_qty").HasPrecision(18, 6).IsRequired();
        builder.Property(d => d.InvoicedQty).HasColumnName("invoiced_qty").HasPrecision(18, 6).IsRequired();
        builder.Property(d => d.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.TaxAmount).HasColumnName("tax_amount").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();
        builder.Ignore(d => d.PendingToInvoice);
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(d => d.PurchaseOrderId).HasDatabaseName("ix_purchase_order_line_order_id");
    }
}
