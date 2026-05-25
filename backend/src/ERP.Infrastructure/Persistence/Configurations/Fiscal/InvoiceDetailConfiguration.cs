using ERP.Domain.Modules.Fiscal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Fiscal;

public sealed class InvoiceDetailConfiguration : IEntityTypeConfiguration<InvoiceDetail>
{
    public void Configure(EntityTypeBuilder<InvoiceDetail> builder)
    {
        builder.ToTable("invoice_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(e => e.LineNo).HasColumnName("line_no").IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(e => e.ProductNameSnapshot).HasColumnName("product_name_snapshot").HasMaxLength(InvoiceDetail.NameMaxLen).IsRequired();
        builder.Property(e => e.SkuSnapshot).HasColumnName("sku_snapshot").HasMaxLength(InvoiceDetail.SkuMaxLen).IsRequired();
        builder.Property(e => e.UnitNameSnapshot).HasColumnName("unit_name_snapshot").HasMaxLength(InvoiceDetail.UnitMaxLen).IsRequired();
        builder.Property(e => e.DescriptionSnapshot).HasColumnName("description_snapshot").HasMaxLength(InvoiceDetail.DescriptionMaxLen).IsRequired();
        builder.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(18, 6);
        builder.Property(e => e.UnitPriceSnapshot).HasColumnName("unit_price_snapshot").HasPrecision(18, 6);
        builder.Property(e => e.DiscountAmount).HasColumnName("discount_amount").HasPrecision(18, 4);
        builder.Property(e => e.TaxCodeSnapshot).HasColumnName("tax_code_snapshot").HasMaxLength(InvoiceDetail.VatCodeMaxLen);
        builder.Property(e => e.TaxRateSnapshot).HasColumnName("tax_rate_snapshot").HasPrecision(8, 4);
        builder.Property(e => e.LineSubtotal).HasColumnName("line_subtotal").HasPrecision(18, 4);
        builder.Property(e => e.LineTax).HasColumnName("line_tax").HasPrecision(18, 4);
        builder.Property(e => e.LineTotal).HasColumnName("line_total").HasPrecision(18, 4);
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();

        builder.HasIndex(e => new { e.SubscriberId, e.InvoiceId, e.LineNo }).IsUnique().HasDatabaseName("uq_invoice_detail_line");
    }
}
