using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.DocTypes;

public class DocTypeSriMapConfiguration : IEntityTypeConfiguration<DocTypeSriMap>
{
    public void Configure(EntityTypeBuilder<DocTypeSriMap> builder)
    {
        builder.ToTable("doc_type_sri_map", schema: "global");
        builder.HasKey(x => x.DocTypeCode);
        builder.Property(x => x.DocTypeCode).HasColumnName("doc_type_code").HasMaxLength(10);
        builder.Property(x => x.SriDocTypeCode).HasColumnName("sri_doc_type_code").HasMaxLength(5);

        builder
            .HasOne<DocType>()
            .WithMany()
            .HasForeignKey(x => x.DocTypeCode)
            .HasPrincipalKey(x => x.Code)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<SriDocType>()
            .WithMany()
            .HasForeignKey(x => x.SriDocTypeCode)
            .HasPrincipalKey(x => x.Code)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new DocTypeSriMap { DocTypeCode = DocTypeCodes.SalesInvoice, SriDocTypeCode = "01" },
            new DocTypeSriMap { DocTypeCode = DocTypeCodes.SalesCreditNote, SriDocTypeCode = "04" },
            new DocTypeSriMap { DocTypeCode = DocTypeCodes.PurchaseCreditNote, SriDocTypeCode = "04" },
            new DocTypeSriMap { DocTypeCode = DocTypeCodes.ExpenseWithholding, SriDocTypeCode = "07" }
        );
    }
}
