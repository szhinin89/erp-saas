using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public class SupplierNoteDetailConfiguration : IEntityTypeConfiguration<SupplierNoteDetail>
{
    public void Configure(EntityTypeBuilder<SupplierNoteDetail> builder)
    {
        builder.ToTable("supplier_note_detail");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.NoteId).HasColumnName("note_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Qty).HasColumnName("qty").HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4);
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.VatCode).HasColumnName("vat_code").HasMaxLength(5);
        builder.Property(x => x.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 4).HasDefaultValue(0m);

        builder.HasOne(x => x.Note)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.NoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
