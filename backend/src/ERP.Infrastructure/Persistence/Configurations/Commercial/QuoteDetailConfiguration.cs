using ERP.Domain.Modules.Commercial.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Commercial;

public sealed class QuoteDetailConfiguration : IEntityTypeConfiguration<QuoteDetail>
{
    public void Configure(EntityTypeBuilder<QuoteDetail> builder)
    {
        builder.ToTable("quote_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.QuoteId).HasColumnName("quote_id").IsRequired();
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(e => e.LineNo).HasColumnName("line_no").IsRequired();
        builder.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(e => e.ProductNameSnapshot).HasColumnName("product_name_snapshot").HasMaxLength(QuoteDetail.NameMaxLen).IsRequired();
        builder.Property(e => e.SkuSnapshot).HasColumnName("sku_snapshot").HasMaxLength(QuoteDetail.SkuMaxLen).IsRequired();
        builder.Property(e => e.UnitNameSnapshot).HasColumnName("unit_name_snapshot").HasMaxLength(QuoteDetail.UnitMaxLen).IsRequired();
        builder.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(18, 6);
        builder.Property(e => e.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 6);
        builder.Property(e => e.TaxRateSnapshot).HasColumnName("tax_rate_snapshot").HasPrecision(8, 4);
        builder.Property(e => e.LineSubtotal).HasColumnName("line_subtotal").HasPrecision(18, 4);
        builder.Property(e => e.LineTax).HasColumnName("line_tax").HasPrecision(18, 4);
        builder.Property(e => e.LineTotal).HasColumnName("line_total").HasPrecision(18, 4);
        builder.Property(e => e.IssueDate).HasColumnName("issue_date").IsRequired();

        builder.HasIndex(e => new { e.SubscriberId, e.QuoteId, e.LineNo })
            .IsUnique()
            .HasDatabaseName("uq_quote_detail_line");
    }
}
