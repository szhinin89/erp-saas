using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class WithholdingDetailConfiguration : IEntityTypeConfiguration<WithholdingDetail>
{
    public void Configure(EntityTypeBuilder<WithholdingDetail> builder)
    {
        builder.ToTable("withholding_detail");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DocId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.TaxType).HasColumnName("tax_type").HasMaxLength(10).IsRequired();
        builder.Property(x => x.RetentionCode).HasColumnName("retention_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.RetainedDocType).HasColumnName("retained_doc_type").HasMaxLength(5);
        builder.Property(x => x.RetainedDocNum).HasColumnName("retained_doc_num").HasMaxLength(20);
        builder.Property(x => x.RetainedDocDate).HasColumnName("retained_doc_date");
        builder.Property(x => x.TaxableBase).HasColumnName("taxable_base").HasPrecision(18, 4);
        builder.Property(x => x.RetentionPct).HasColumnName("retention_pct").HasPrecision(7, 4);
        builder.Property(x => x.AmountRetained).HasColumnName("amount_retained").HasPrecision(18, 4);
        builder.Property(x => x.TaxSupportCode).HasColumnName("tax_support_code").HasMaxLength(5);

        builder.HasIndex(x => x.DocId).HasDatabaseName("idx_wh_det_doc");

        builder.HasOne(x => x.Doc)
            .WithMany(x => x.WhLines).HasForeignKey(x => x.DocId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.TaxSupport)
            .WithMany().HasForeignKey(x => x.TaxSupportCode).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}
