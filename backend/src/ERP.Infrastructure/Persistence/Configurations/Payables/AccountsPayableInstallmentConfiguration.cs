using ERP.Domain.Modules.Payables.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Payables;

public sealed class AccountsPayableInstallmentConfiguration
    : IEntityTypeConfiguration<AccountsPayableInstallment>
{
    public void Configure(EntityTypeBuilder<AccountsPayableInstallment> builder)
    {
        builder.ToTable("accounts_payable_installments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.AccountsPayableId).HasColumnName("accounts_payable_id").IsRequired();
        builder.Property(x => x.InstallmentNumber).HasColumnName("installment_number").IsRequired();
        builder.Property(x => x.DueDate).HasColumnName("due_date").IsRequired();
        builder
            .Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.PaidAmount)
            .HasColumnName("paid_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.RetainedAmount)
            .HasColumnName("retained_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.ReturnCreditAmount)
            .HasColumnName("return_credit_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.SupplierCreditAmount)
            .HasColumnName("supplier_credit_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.CreditNoteAmount)
            .HasColumnName("credit_note_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Ignore(x => x.OutstandingAmount);

        builder
            .HasIndex(x => new { x.TenantId, x.AccountsPayableId })
            .HasDatabaseName("ix_accounts_payable_installments_tenant_payable");

        builder
            .HasIndex(x => new { x.TenantId, x.DueDate })
            .HasDatabaseName("ix_accounts_payable_installments_tenant_duedate");

        builder
            .HasIndex(x => new { x.AccountsPayableId, x.InstallmentNumber })
            .IsUnique()
            .HasDatabaseName("uq_accounts_payable_installments_payable_number");
    }
}
