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
            {
                t.HasCheckConstraint(
                    "chk_supplier_payment_methods_destination_xor",
                    "(\"company_bank_account_id\" IS NOT NULL AND \"cash_register_id\" IS NULL) "
                        + "OR (\"company_bank_account_id\" IS NULL AND \"cash_register_id\" IS NOT NULL)"
                );
                // 02A — toda fuente bancaria conserva su fecha efectiva real (conciliación futura).
                t.HasCheckConstraint(
                    "chk_supplier_payment_methods_bank_transaction_date",
                    "\"company_bank_account_id\" IS NULL OR \"transaction_date\" IS NOT NULL"
                );
            }
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
        // ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A
        builder.Property(x => x.TransactionDate).HasColumnName("transaction_date");
        builder.Property(x => x.CashSessionId).HasColumnName("cash_session_id");
        builder.Property(x => x.CashMovementId).HasColumnName("cash_movement_id");

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
        // Un CashMovement de pago a proveedor pertenece a exactamente una fuente.
        builder
            .HasIndex(x => x.CashMovementId)
            .IsUnique()
            .HasFilter("cash_movement_id IS NOT NULL")
            .HasDatabaseName("ux_supplier_payment_methods_cash_movement");
        builder
            .HasIndex(x => x.CashSessionId)
            .HasDatabaseName("ix_supplier_payment_methods_cash_session");
        // Base de la futura conciliación bancaria: Cuenta + Fecha + Referencia (+ Monto).
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyBankAccountId,
                x.TransactionDate,
                x.ReferenceNumber,
            })
            .HasFilter("company_bank_account_id IS NOT NULL")
            .HasDatabaseName("ix_supplier_payment_methods_bank_reconciliation");
    }
}
