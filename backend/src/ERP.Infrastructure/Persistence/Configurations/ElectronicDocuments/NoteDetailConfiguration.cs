using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class NoteDetailConfiguration : IEntityTypeConfiguration<NoteDetail>
{
    public void Configure(EntityTypeBuilder<NoteDetail> builder)
    {
        builder.ToTable("note_detail");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DocId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.ProductCode).HasColumnName("product_code").HasMaxLength(100);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(300).IsRequired();
        builder.Property(x => x.UnitCode).HasColumnName("unit_code").HasMaxLength(20);
        builder.Property(x => x.Qty).HasColumnName("qty").HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4);
        builder.Property(x => x.DiscountPct).HasColumnName("discount_pct").HasPrecision(6, 2).HasDefaultValue(0m);
        builder.Property(x => x.VatCode).HasColumnName("vat_code").HasMaxLength(5).IsRequired();
        builder.Property(x => x.VatPercentage).HasColumnName("vat_percentage").HasPrecision(6, 2).HasDefaultValue(0m);
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue((short)0);

        builder.HasIndex(x => x.DocId).HasDatabaseName("idx_note_det_doc");

        builder.HasOne(x => x.Doc)
            .WithMany(x => x.NoteLines).HasForeignKey(x => x.DocId).OnDelete(DeleteBehavior.Cascade);
    }
}
