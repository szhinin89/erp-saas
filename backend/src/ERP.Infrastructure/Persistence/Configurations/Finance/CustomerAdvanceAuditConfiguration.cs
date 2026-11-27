using ERP.Domain.Modules.Finance.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

/// <summary>Mapeo EF de <see cref="CustomerAdvanceAudit"/> (ADR-022, Entity Audit) — ADR-052 §11.</summary>
public sealed class CustomerAdvanceAuditConfiguration
    : IEntityTypeConfiguration<CustomerAdvanceAudit>
{
    public void Configure(EntityTypeBuilder<CustomerAdvanceAudit> builder)
    {
        builder.ConfigureAuditBase("customer_advance_audit");

        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.MovementId).HasColumnName("movement_id").IsRequired();
        builder
            .Property(x => x.MovementType)
            .HasColumnName("movement_type")
            .HasConversion<int>()
            .IsRequired();
        builder
            .Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.BalanceBefore)
            .HasColumnName("balance_before")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.BalanceAfter)
            .HasColumnName("balance_after")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.StatusBefore)
            .HasColumnName("status_before")
            .HasMaxLength(CustomerAdvanceAudit.StatusMaxLen);
        builder
            .Property(x => x.StatusAfter)
            .HasColumnName("status_after")
            .HasMaxLength(CustomerAdvanceAudit.StatusMaxLen)
            .IsRequired();
        builder.Property(x => x.ClientRequestId).HasColumnName("client_request_id").IsRequired();
        builder
            .Property(x => x.EffectiveDate)
            .HasColumnName("effective_date")
            .HasColumnType("date")
            .IsRequired();
        builder.Property(x => x.EvidenceDate).HasColumnName("evidence_date").HasColumnType("date");
        builder
            .Property(x => x.Reference)
            .HasColumnName("reference")
            .HasMaxLength(CustomerAdvanceMovement.ReferenceMaxLen);
        builder.Property(x => x.ReceivableId).HasColumnName("receivable_id");
        builder.Property(x => x.InstallmentId).HasColumnName("installment_id");
        builder.Property(x => x.ReversalOfMovementId).HasColumnName("reversal_of_movement_id");
        builder.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id").IsRequired();
        builder.Property(x => x.CompanyBankAccountId).HasColumnName("company_bank_account_id");
        builder.Property(x => x.CashRegisterId).HasColumnName("cash_register_id");
        builder.Property(x => x.CashSessionId).HasColumnName("cash_session_id");
        builder.Property(x => x.CashMovementId).HasColumnName("cash_movement_id");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CustomerId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_customer_advance_audit_customer_occurred_at");
    }
}
