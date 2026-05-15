using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class DeliveryDetailConfiguration : IEntityTypeConfiguration<DeliveryDetail>
{
    public void Configure(EntityTypeBuilder<DeliveryDetail> builder)
    {
        builder.ToTable("delivery_detail");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DocId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Qty).HasColumnName("qty").HasPrecision(18, 4);
        builder.Property(x => x.UnitCode).HasColumnName("unit_code").HasMaxLength(20);

        builder.HasOne(x => x.Doc)
            .WithMany(x => x.DeliveryLines).HasForeignKey(x => x.DocId).OnDelete(DeleteBehavior.Cascade);
    }
}
