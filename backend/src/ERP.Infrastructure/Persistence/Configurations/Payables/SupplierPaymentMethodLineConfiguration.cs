using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Payables;

public sealed class SupplierPaymentMethodLineConfiguration : IEntityTypeConfiguration<SupplierPaymentMethodLine>
{
    public void Configure(EntityTypeBuilder<SupplierPaymentMethodLine> builder)
    {
        builder.ToTable(
            "supplier_payment_methods",
            t =>
                t.HasCheckConstraint(
                    "chk_supplier_payment_methods_destination_xor",
                    "(\"company_bank_account_id\" IS NOT NULL AND \"cash_register_id\" IS NULL) "
                        + "OR (\"company_bank_account_id\" IS NULL AND \"cash_register_id\" IS NOT NULL)"
                )
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SupplierPaymentId).HasColumnName("supplier_payment_id").IsRequired();
        builder.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id").IsRequired();
        builder.Property(x => x.CompanyBankAccountId).HasColumnName("company_bank_account_id");
        builder.Property(x => x.CashRegisterId).HasColumnName("cash_register_id");
        builder
            .Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.ReferenceNumber)
            .HasColumnName("reference_number")
            .HasMaxLength(60);
        builder.Property(x => x.CheckNumber).HasColumnName("check_number").HasMaxLength(30);
        builder.Property(x => x.CheckDate).HasColumnName("check_date");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);

        builder
            .HasOne<PaymentMethod>()
            .WithMany()
            .HasForeignKey(x => x.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CompanyBankAccount>()
            .WithMany()
            .HasForeignKey(x => x.CompanyBankAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CashRegister>()
            .WithMany()
            .HasForeignKey(x => x.CashRegisterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.TenantId, x.SupplierPaymentId })
            .HasDatabaseName("ix_supplier_payment_methods_tenant_payment");
        builder
            .HasIndex(x => x.PaymentMethodId)
            .HasDatabaseName("ix_supplier_payment_methods_payment_method");
        builder
            .HasIndex(x => x.CompanyBankAccountId)
            .HasDatabaseName("ix_supplier_payment_methods_bank_account");
        builder
            .HasIndex(x => x.CashRegisterId)
            .HasDatabaseName("ix_supplier_payment_methods_cash_register");
    }
}
