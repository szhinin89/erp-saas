using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public class ReceivedWhDetailConfiguration : IEntityTypeConfiguration<ReceivedWhDetail>
{
    public void Configure(EntityTypeBuilder<ReceivedWhDetail> builder)
    {
        builder.ToTable("received_wh_detail");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.WithholdingId).HasColumnName("withholding_id").IsRequired();
        builder.Property(x => x.TaxType).HasColumnName("tax_type").HasMaxLength(10).IsRequired();
        builder.Property(x => x.RetentionCode).HasColumnName("retention_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.TaxableBase).HasColumnName("taxable_base").HasPrecision(18, 4);
        builder.Property(x => x.RetentionPct).HasColumnName("retention_pct").HasPrecision(7, 4);
        builder.Property(x => x.AmountRetained).HasColumnName("amount_retained").HasPrecision(18, 4);
        builder.Property(x => x.RelatedInvoiceNum).HasColumnName("related_invoice_num").HasMaxLength(50);

        builder.HasOne(x => x.Withholding)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.WithholdingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
