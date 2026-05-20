using ERP.Domain.Modules.Purchasing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchasing;

public sealed class PurchNoteLineConfiguration : IEntityTypeConfiguration<PurchNoteLine>
{
    public void Configure(EntityTypeBuilder<PurchNoteLine> builder)
    {
        builder.ToTable("purch_note_line");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(d => d.PurchNoteId).HasColumnName("purch_note_id").IsRequired();
        builder.Property(d => d.ProductId).HasColumnName("product_id");
        builder.Property(d => d.SupplierProductCode).HasColumnName("supplier_product_code").HasMaxLength(PurchNoteLine.SupplierProductCodeMaxLen);
        builder.Property(d => d.Description).HasColumnName("description").HasMaxLength(PurchNoteLine.DescriptionMaxLen).IsRequired();
        builder.Property(d => d.Quantity).HasColumnName("quantity").HasPrecision(18, 6).IsRequired();
        builder.Property(d => d.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(d => d.PurchNoteId).HasDatabaseName("ix_purch_note_line_note_id");
    }
}
