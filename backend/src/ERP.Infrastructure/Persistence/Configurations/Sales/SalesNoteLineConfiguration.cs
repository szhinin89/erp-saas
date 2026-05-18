using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesNoteLineConfiguration : IEntityTypeConfiguration<SalesNoteLine>
{
    public void Configure(EntityTypeBuilder<SalesNoteLine> builder)
    {
        builder.ToTable("sales_note_line");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.SalesNoteId).HasColumnName("sales_note_id").IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(e => e.ProductCode).HasColumnName("product_code")
               .HasMaxLength(SalesNoteLine.ProductCodeMaxLen).IsRequired()
               .HasDefaultValue("");
        builder.Property(e => e.VatCode).HasColumnName("vat_code")
               .HasMaxLength(SalesNoteLine.VatCodeMaxLen).IsRequired().HasDefaultValue("0");
        builder.Property(e => e.VatPercentage).HasColumnName("vat_percentage")
               .HasPrecision(5, 2).IsRequired().HasDefaultValue(0m);
        builder.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.VatTotal).HasColumnName("vat_total").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Total).HasColumnName("total").HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(SalesNoteLine.DescriptionMaxLen).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.TenantId, e.SalesNoteId }).HasDatabaseName("ix_sales_note_line_tenant_note");
    }
}
