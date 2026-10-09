using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesReceivableConfiguration : IEntityTypeConfiguration<SalesReceivable>
{
    public void Configure(EntityTypeBuilder<SalesReceivable> builder)
    {
        // IL-5A — coherencia por origen reforzada en BD: una CxC de factura siempre tiene factura y
        // nunca datos de saldo inicial; un saldo inicial nunca tiene factura y siempre trae número,
        // fecha de emisión, sucursal y lote de origen.
        builder.ToTable(
            "sales_receivables",
            t =>
                t.HasCheckConstraint(
                    "chk_sales_receivables_origin_shape",
                    "(origin = 1 AND invoice_id IS NOT NULL AND document_number IS NULL "
                        + "AND document_number_normalized IS NULL AND issue_date IS NULL "
                        + "AND branch_id IS NULL AND import_batch_id IS NULL) OR "
                        + "(origin = 2 AND invoice_id IS NULL AND document_number IS NOT NULL "
                        + "AND document_number_normalized IS NOT NULL AND document_number_normalized <> '' "
                        + "AND issue_date IS NOT NULL AND branch_id IS NOT NULL AND import_batch_id IS NOT NULL)"
                )
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        // Filas existentes = facturas: el default 1 (Invoice) las clasifica en la migración.
        builder
            .Property(x => x.Origin)
            .HasColumnName("origin")
            .HasConversion<int>()
            .HasDefaultValue(SalesReceivableOrigin.Invoice)
            .IsRequired();
        builder.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder
            .Property(x => x.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(SalesReceivable.DocumentNumberMaxLen);
        builder
            .Property(x => x.DocumentNumberNormalized)
            .HasColumnName("document_number_normalized")
            .HasMaxLength(SalesReceivable.DocumentNumberMaxLen);
        builder.Property(x => x.IssueDate).HasColumnName("issue_date");
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.ImportBatchId).HasColumnName("import_batch_id");

        builder
            .Property(x => x.OriginalAmount)
            .HasColumnName("original_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.PaidAmount)
            .HasColumnName("paid_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(SalesReceivable.StatusMaxLen)
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(x => x.BalanceDue);

        builder
            .HasMany(x => x.Installments)
            .WithOne()
            .HasForeignKey(x => x.ReceivableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<SalesInvoice>()
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<ImportBatch>()
            .WithMany()
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => x.InvoiceId)
            .IsUnique()
            .HasDatabaseName("uq_sales_receivables_invoice");

        // IL-5A — el mismo documento (normalizado) de un cliente no puede cargarse dos veces como
        // saldo inicial en la empresa, aunque dos confirmaciones concurrentes pasen la validación.
        // La comparación contra números de factura vive en la validación de la Carga Inicial.
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.CustomerId, x.DocumentNumberNormalized })
            .IsUnique()
            .HasFilter("origin = 2")
            .HasDatabaseName("uq_sales_receivables_initial_balance_document");

        builder
            .HasIndex(x => x.ImportBatchId)
            .HasFilter("import_batch_id IS NOT NULL")
            .HasDatabaseName("ix_sales_receivables_import_batch");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_sales_receivables_tenant_company");

        builder
            .HasIndex(x => new { x.TenantId, x.CustomerId })
            .HasDatabaseName("ix_sales_receivables_tenant_customer");

        builder
            .HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("ix_sales_receivables_tenant_status");
    }
}
