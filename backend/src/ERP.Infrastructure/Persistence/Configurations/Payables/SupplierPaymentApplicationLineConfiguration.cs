using ERP.Domain.Modules.Payables.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Payables;

public sealed class SupplierPaymentApplicationLineConfiguration
    : IEntityTypeConfiguration<SupplierPaymentApplicationLine>
{
    public void Configure(EntityTypeBuilder<SupplierPaymentApplicationLine> builder)
    {
        builder.ToTable("supplier_payment_applications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SupplierPaymentId).HasColumnName("supplier_payment_id").IsRequired();
        builder
            .Property(x => x.AccountsPayableInstallmentId)
            .HasColumnName("accounts_payable_installment_id")
            .IsRequired();
        builder
            .Property(x => x.AmountApplied)
            .HasColumnName("amount_applied")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .HasOne<AccountsPayableInstallment>()
            .WithMany()
            .HasForeignKey(x => x.AccountsPayableInstallmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.TenantId, x.SupplierPaymentId })
            .HasDatabaseName("ix_supplier_payment_applications_tenant_payment");
        builder
            .HasIndex(x => x.AccountsPayableInstallmentId)
            .HasDatabaseName("ix_supplier_payment_applications_installment");
    }
}
