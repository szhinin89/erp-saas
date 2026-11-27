using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// Mapeo EF de <see cref="CustomerAdvanceRefundTransaction"/> — ADR-052 §4. Relación 1:1 con su
/// movimiento. Tipos: 1=Refund, 2=RefundReversed.
/// </summary>
public sealed class CustomerAdvanceRefundTransactionConfiguration
    : IEntityTypeConfiguration<CustomerAdvanceRefundTransaction>
{
    public void Configure(EntityTypeBuilder<CustomerAdvanceRefundTransaction> builder)
    {
        builder.ToTable(
            "customer_advance_refund_transactions",
            t =>
            {
                t.HasCheckConstraint(
                    "chk_customer_advance_refund_transactions_amount_positive",
                    "\"amount\" > 0"
                );
                t.HasCheckConstraint(
                    "chk_customer_advance_refund_transactions_destination_xor",
                    "(\"company_bank_account_id\" IS NULL) <> (\"cash_register_id\" IS NULL)"
                );
                t.HasCheckConstraint(
                    "chk_customer_advance_refund_transactions_original",
                    "(\"transaction_type\" = 1 AND \"original_transaction_id\" IS NULL) "
                        + "OR (\"transaction_type\" = 2 AND \"original_transaction_id\" IS NOT NULL)"
                );
                t.HasCheckConstraint(
                    "chk_customer_advance_refund_transactions_cash_link",
                    "(\"cash_session_id\" IS NULL AND \"cash_movement_id\" IS NULL) "
                        + "OR (\"cash_register_id\" IS NOT NULL AND \"cash_session_id\" IS NOT NULL AND \"cash_movement_id\" IS NOT NULL)"
                );
            }
        );

        builder.HasKey(x => x.Id);
        builder
            .HasAlternateKey(x => new
            {
                x.Id,
                x.CustomerAdvanceId,
                x.TenantId,
                x.CompanyId,
            })
            .HasName("ak_customer_advance_refund_transactions_id_scope");

        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        Immutable(builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired());
        Immutable(builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired());
        Immutable(
            builder
                .Property(x => x.CustomerAdvanceId)
                .HasColumnName("customer_advance_id")
                .IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.CustomerAdvanceMovementId)
                .HasColumnName("customer_advance_movement_id")
                .IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.TransactionType)
                .HasColumnName("transaction_type")
                .HasConversion<int>()
                .IsRequired()
        );
        Immutable(
            builder.Property(x => x.OriginalTransactionId).HasColumnName("original_transaction_id")
        );
        Immutable(
            builder.Property(x => x.CompanyBankAccountId).HasColumnName("company_bank_account_id")
        );
        Immutable(builder.Property(x => x.CashRegisterId).HasColumnName("cash_register_id"));
        Immutable(
            builder.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id").IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(18,2)")
                .IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.CurrencyCode)
                .HasColumnName("currency_code")
                .HasMaxLength(CustomerAdvance.CurrencyCodeLen)
                .IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.EffectiveDate)
                .HasColumnName("effective_date")
                .HasColumnType("date")
                .IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.Reference)
                .HasColumnName("reference")
                .HasMaxLength(CustomerAdvanceMovement.ReferenceMaxLen)
                .IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.Reason)
                .HasColumnName("reason")
                .HasMaxLength(CustomerAdvanceMovement.ReasonMaxLen)
                .IsRequired()
        );
        builder.Property(x => x.CashSessionId).HasColumnName("cash_session_id");
        builder.Property(x => x.CashMovementId).HasColumnName("cash_movement_id");
        Immutable(
            builder
                .Property(x => x.CreatedByUserId)
                .HasColumnName("created_by_user_id")
                .IsRequired()
        );
        Immutable(
            builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired()
        );
        Immutable(
            builder.Property(x => x.ClientRequestId).HasColumnName("client_request_id").IsRequired()
        );
        Immutable(
            builder
                .Property(x => x.PayloadHash)
                .HasColumnName("payload_hash")
                .HasMaxLength(ERP.Domain.Common.ClientRequestKey.PayloadHashLength)
                .IsRequired()
        );

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // ── Relationships ────────────────────────────────────────────────
        builder
            .HasOne<CustomerAdvanceMovement>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.CustomerAdvanceMovementId,
                x.CustomerAdvanceId,
                x.TenantId,
                x.CompanyId,
            })
            .HasPrincipalKey(x => new
            {
                x.Id,
                x.CustomerAdvanceId,
                x.TenantId,
                x.CompanyId,
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_customer_advance_refund_transactions_movement_scope");
        builder
            .HasOne<CustomerAdvanceRefundTransaction>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.OriginalTransactionId,
                x.CustomerAdvanceId,
                x.TenantId,
                x.CompanyId,
            })
            .HasPrincipalKey(x => new
            {
                x.Id,
                x.CustomerAdvanceId,
                x.TenantId,
                x.CompanyId,
            })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_customer_advance_refund_transactions_original_scope");
        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<PaymentMethod>()
            .WithMany()
            .HasForeignKey(x => x.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CompanyBankAccount>()
            .WithMany()
            .HasForeignKey(x => x.CompanyBankAccountId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CashRegister>()
            .WithMany()
            .HasForeignKey(x => x.CashRegisterId)
            .IsRequired(false)
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
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.CustomerAdvanceId,
            })
            .HasDatabaseName("ix_customer_advance_refund_transactions_tenant_company_advance");
        builder
            .HasIndex(x => x.CustomerAdvanceMovementId)
            .IsUnique()
            .HasDatabaseName("uq_customer_advance_refund_transactions_movement");
        builder
            .HasIndex(x => x.OriginalTransactionId)
            .IsUnique()
            .HasFilter("\"original_transaction_id\" IS NOT NULL")
            .HasDatabaseName("uq_customer_advance_refund_transactions_original");
        builder
            .HasIndex(x => new { x.TenantId, x.ClientRequestId })
            .IsUnique()
            .HasDatabaseName("uq_customer_advance_refund_transactions_tenant_client_request");
    }

    private static void Immutable<TProperty>(PropertyBuilder<TProperty> property) =>
        property.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
}
