using ERP.Domain.Modules.ElectronicDocuments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.ElectronicDocuments;

public class DocTaxConfiguration : IEntityTypeConfiguration<DocTax>
{
    public void Configure(EntityTypeBuilder<DocTax> builder)
    {
        builder.ToTable("doc_tax");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DocId).HasColumnName("doc_id").IsRequired();
        builder.Property(x => x.TaxType).HasColumnName("tax_type");
        builder.Property(x => x.TaxCode).HasColumnName("tax_code").HasMaxLength(10).IsRequired();
        builder.Property(x => x.TaxableBase).HasColumnName("taxable_base").HasPrecision(18, 4);
        builder.Property(x => x.Percentage).HasColumnName("percentage").HasPrecision(7, 4);
        builder.Property(x => x.TaxAmount).HasColumnName("tax_amount").HasPrecision(18, 4);

        builder.HasIndex(x => x.DocId).HasDatabaseName("idx_doc_tax");

        builder.HasOne(x => x.Doc)
            .WithMany(x => x.Taxes).HasForeignKey(x => x.DocId).OnDelete(DeleteBehavior.Cascade);
    }
}
