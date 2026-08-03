using ERP.Domain.Modules.Purchases.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>Mapeo EF de <see cref="SupplierCreditAudit"/> (ADR-022, Entity Audit) — diseño P0-02 §20.1/§20.1bis.</summary>
public sealed class SupplierCreditAuditConfiguration : IEntityTypeConfiguration<SupplierCreditAudit>
{
    public void Configure(EntityTypeBuilder<SupplierCreditAudit> builder)
    {
        builder.ConfigureAuditBase("supplier_credit_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.MovementType).HasColumnName("movement_type").HasConversion<int?>();
        builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.BalanceBefore)
            .HasColumnName("balance_before")
            .HasColumnType("numeric(18,2)");
        builder
            .Property(x => x.BalanceAfter)
            .HasColumnName("balance_after")
            .HasColumnType("numeric(18,2)");
        builder.Property(x => x.StatusBefore).HasColumnName("status_before").HasMaxLength(20);
        builder.Property(x => x.StatusAfter).HasColumnName("status_after").HasMaxLength(20);
        builder.Property(x => x.TargetPurchasePayableId).HasColumnName("target_purchase_payable_id");
        builder.Property(x => x.SourcePurchaseReturnId).HasColumnName("source_purchase_return_id");

        builder.Property(x => x.FinancialDestinationId).HasColumnName("financial_destination_id");
        builder
            .Property(x => x.FinancialDestinationCodeSnapshot)
            .HasColumnName("financial_destination_code_snapshot")
            .HasMaxLength(30);
        builder
            .Property(x => x.DestinationTypeCodeSnapshot)
            .HasColumnName("destination_type_code_snapshot")
            .HasMaxLength(20);
        builder.Property(x => x.AccountingAccountId).HasColumnName("accounting_account_id");
        builder.Property(x => x.CashRegisterId).HasColumnName("cash_register_id");
        builder.Property(x => x.CashSessionId).HasColumnName("cash_session_id");
        builder.Property(x => x.CashMovementId).HasColumnName("cash_movement_id");
        builder.Property(x => x.PaymentMethodCode).HasColumnName("payment_method_code").HasMaxLength(20);
        builder
            .Property(x => x.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(100);
        builder.Property(x => x.EffectiveDate).HasColumnName("effective_date").HasColumnType("date");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.SupplierId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_supplier_credit_audit_supplier_occurred_at");
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.SourcePurchaseReturnId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_supplier_credit_audit_source_return_occurred_at");
    }
}
