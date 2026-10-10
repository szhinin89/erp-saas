using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.Payables.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Payables;

public sealed class AccountsPayableConfiguration : IEntityTypeConfiguration<AccountsPayable>
{
    public void Configure(EntityTypeBuilder<AccountsPayable> builder)
    {
        // IL-6A — coherencia por origen reforzada en BD: solo un saldo inicial (origin_type = 3,
        // AccountsPayableOriginType.InitialBalance) lleva número normalizado y lote de importación,
        // y siempre los lleva; Compra/Gasto nunca.
        builder.ToTable(
            "accounts_payables",
            t =>
                t.HasCheckConstraint(
                    "chk_accounts_payables_initial_balance_shape",
                    "(origin_type <> 3 AND document_number_normalized IS NULL AND import_batch_id IS NULL) OR "
                        + "(origin_type = 3 AND document_number_normalized IS NOT NULL "
                        + "AND document_number_normalized <> '' AND import_batch_id IS NOT NULL)"
                )
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder
            .Property(x => x.OriginType)
            .HasColumnName("origin_type")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(x => x.OriginId).HasColumnName("origin_id").IsRequired();
        builder
            .Property(x => x.DocumentType)
            .HasColumnName("document_type")
            .HasMaxLength(AccountsPayable.DocumentTypeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(AccountsPayable.DocumentNumberMaxLen)
            .IsRequired();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date").IsRequired();
        builder.Property(x => x.AccountingDate).HasColumnName("accounting_date").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder
            .Property(x => x.DocumentNumberNormalized)
            .HasColumnName("document_number_normalized")
            .HasMaxLength(AccountsPayable.DocumentNumberMaxLen);
        builder.Property(x => x.ImportBatchId).HasColumnName("import_batch_id");
        // ZH-RETENTION-SRI-ANNULMENT-01 — retención de la CxP por una anulación en trámite ante el SRI.
        builder.Property(x => x.AnnulmentHoldRequestId).HasColumnName("annulment_hold_request_id");
        builder.Ignore(x => x.IsOnAnnulmentHold);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Derivadas de las cuotas — nunca columnas propias (mismo criterio que
        // ExpenseDocument.Subtotal/GrandTotal). PAYABLES-PURCHASE-MIGRATION-10: los 6 acumuladores
        // que antes vivían en PurchasePayable (columnas propias) ahora se derivan sumando
        // AccountsPayableInstallment — decisión funcional: la cuota es la única fuente de saldo.
        builder.Ignore(x => x.TotalAmount);
        builder.Ignore(x => x.PaidAmount);
        builder.Ignore(x => x.RetainedAmount);
        builder.Ignore(x => x.ReturnCreditAmount);
        builder.Ignore(x => x.SupplierCreditAmount);
        builder.Ignore(x => x.CreditNoteAmount);
        builder.Ignore(x => x.OutstandingAmount);

        builder
            .HasMany(x => x.Installments)
            .WithOne()
            .HasForeignKey(x => x.AccountsPayableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<ImportBatch>()
            .WithMany()
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.OriginType,
                x.OriginId,
            })
            .IsUnique()
            .HasDatabaseName("uq_accounts_payables_tenant_company_origin");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.SupplierId,
                x.Status,
            })
            .HasDatabaseName("ix_accounts_payables_tenant_company_supplier_status");

        // IL-6A — el mismo documento (normalizado) de un proveedor no puede cargarse dos veces como
        // saldo inicial en la empresa, aunque dos confirmaciones concurrentes pasen la validación.
        // La comparación contra CxP de Compras/Gastos vive en la validación de la Carga Inicial.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.SupplierId,
                x.DocumentNumberNormalized,
            })
            .IsUnique()
            .HasFilter("origin_type = 3")
            .HasDatabaseName("uq_accounts_payables_initial_balance_document");

        builder
            .HasIndex(x => x.ImportBatchId)
            .HasFilter("import_batch_id IS NOT NULL")
            .HasDatabaseName("ix_accounts_payables_import_batch");
    }
}
