using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// Mapeo EF de <see cref="SupplierCreditRefundTransaction"/> — diseño P0-02 §6.4/§6.4bis, Fase 2.
/// Relación 1:1 estricta con <see cref="Purchases.Entities.SupplierCreditMovement"/> reforzada por
/// índice único — <c>SupplierCreditMovement</c> no tiene columna de referencia inversa (§7.5).
/// </summary>
public sealed class SupplierCreditRefundTransactionConfiguration
    : IEntityTypeConfiguration<SupplierCreditRefundTransaction>
{
    public void Configure(EntityTypeBuilder<SupplierCreditRefundTransaction> builder)
    {
        builder.ToTable("supplier_credit_refund_transactions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.SupplierCreditId).HasColumnName("supplier_credit_id").IsRequired();
        builder
            .Property(x => x.SupplierCreditMovementId)
            .HasColumnName("supplier_credit_movement_id")
            .IsRequired();
        builder
            .Property(x => x.TransactionTypeCode)
            .HasColumnName("transaction_type_code")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(x => x.OriginalTransactionId).HasColumnName("original_transaction_id");
        builder.Property(x => x.FinancialDestinationId).HasColumnName("financial_destination_id").IsRequired();
        builder.Property(x => x.AccountingAccountId).HasColumnName("accounting_account_id").IsRequired();
        builder
            .Property(x => x.PaymentMethodCode)
            .HasColumnName("payment_method_code")
            .HasMaxLength(SupplierCreditRefundTransaction.PaymentMethodCodeMaxLen)
            .IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
        builder
            .Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(SupplierCreditRefundTransaction.CurrencyCodeMaxLen)
            .IsRequired();
        builder.Property(x => x.EffectiveDate).HasColumnName("effective_date").HasColumnType("date").IsRequired();
        builder
            .Property(x => x.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(SupplierCreditRefundTransaction.ExternalReferenceMaxLen);
        builder
            .Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(SupplierCreditRefundTransaction.ReasonMaxLen);
        builder.Property(x => x.CashSessionId).HasColumnName("cash_session_id");
        builder.Property(x => x.CashMovementId).HasColumnName("cash_movement_id");

        builder
            .Property(x => x.FinancialDestinationCodeSnapshot)
            .HasColumnName("financial_destination_code_snapshot")
            .HasMaxLength(SupplierCreditRefundTransaction.SnapshotMaxLen)
            .IsRequired();
        builder
            .Property(x => x.FinancialDestinationNameSnapshot)
            .HasColumnName("financial_destination_name_snapshot")
            .HasMaxLength(SupplierCreditRefundTransaction.SnapshotMaxLen)
            .IsRequired();
        builder
            .Property(x => x.DestinationTypeCodeSnapshot)
            .HasColumnName("destination_type_code_snapshot")
            .HasMaxLength(SupplierCreditRefundTransaction.SnapshotMaxLen)
            .IsRequired();
        builder
            .Property(x => x.AccountingAccountCodeSnapshot)
            .HasColumnName("accounting_account_code_snapshot")
            .HasMaxLength(SupplierCreditRefundTransaction.SnapshotMaxLen)
            .IsRequired();

        // ── Idempotencia (§6.4, §16.2) ──────────────────────────────────────
        builder.Property(x => x.ClientRequestId).HasColumnName("client_request_id").IsRequired();
        builder
            .Property(x => x.PayloadHash)
            .HasColumnName("payload_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // ── Relationships ────────────────────────────────────────────────
        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<SupplierCredit>()
            .WithMany()
            .HasForeignKey(x => x.SupplierCreditId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<SupplierCreditMovement>()
            .WithMany()
            .HasForeignKey(x => x.SupplierCreditMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<SupplierCreditRefundTransaction>()
            .WithMany()
            .HasForeignKey(x => x.OriginalTransactionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<CompanyFinancialDestination>()
            .WithMany()
            .HasForeignKey(x => x.FinancialDestinationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountingAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<CashSession>()
            .WithMany()
            .HasForeignKey(x => x.CashSessionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<CashMovement>()
            .WithMany()
            .HasForeignKey(x => x.CashMovementId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.SupplierCreditId })
            .HasDatabaseName("ix_supplier_credit_refund_transactions_tenant_company_credit");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.AccountingAccountId })
            .HasDatabaseName("ix_supplier_credit_refund_transactions_tenant_company_account");

        // Relación 1:1 estricta con el movimiento que la origina.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.SupplierCreditMovementId,
            })
            .IsUnique()
            .HasDatabaseName("uq_supplier_credit_refund_transactions_movement");

        // Una sola reversa por ingreso.
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.OriginalTransactionId,
            })
            .IsUnique()
            .HasFilter("\"transaction_type_code\" = 2")
            .HasDatabaseName("uq_supplier_credit_refund_transactions_original");

        builder
            .HasIndex(x => new { x.TenantId, x.ClientRequestId })
            .IsUnique()
            .HasDatabaseName("uq_supplier_credit_refund_transactions_tenant_client_request_id");
    }
}
