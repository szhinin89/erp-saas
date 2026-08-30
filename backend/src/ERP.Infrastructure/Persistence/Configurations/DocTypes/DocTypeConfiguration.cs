using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.DocTypes;

public class DocTypeConfiguration : IEntityTypeConfiguration<DocType>
{
    public void Configure(EntityTypeBuilder<DocType> builder)
    {
        builder.ToTable("doc_type", schema: "global");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(10);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.HasData(
            new DocType { Code = DocTypeCodes.SalesInvoice, Name = "Factura de Venta", IsActive = true },
            new DocType
            {
                Code = DocTypeCodes.SalesCreditNote,
                Name = "Nota de Crédito de Venta",
                IsActive = true,
            },
            new DocType { Code = DocTypeCodes.PurchaseInvoice, Name = "Factura de Compra", IsActive = true },
            new DocType
            {
                Code = DocTypeCodes.PurchaseCreditNote,
                Name = "Nota de Crédito de Compra",
                IsActive = true,
            },
            new DocType { Code = DocTypeCodes.ExpenseDocument, Name = "Documento de Gasto", IsActive = true },
            new DocType
            {
                Code = DocTypeCodes.ExpenseWithholding,
                Name = "Retención en Gasto",
                IsActive = true,
            },
            new DocType { Code = DocTypeCodes.SupplierPayment, Name = "Pago a Proveedor", IsActive = true },
            new DocType { Code = DocTypeCodes.CustomerCollection, Name = "Cobro a Cliente", IsActive = true },
            new DocType
            {
                Code = DocTypeCodes.ManualJournalEntry,
                Name = "Asiento Contable Manual",
                IsActive = true,
            },
            new DocType
            {
                Code = DocTypeCodes.InventoryAdjustment,
                Name = "Ajuste de Inventario",
                IsActive = true,
            }
        );
    }
}
