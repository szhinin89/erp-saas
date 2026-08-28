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
        builder.ToTable("supplier_payment_methods");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SupplierPaymentId).HasColumnName("supplier_payment_id").IsRequired();
        builder.Property(x => x.PaymentMethodId).HasColumnName("payment_method_id").IsRequired();
        builder
            .Property(x => x.FinancialDestinationId)
            .HasColumnName("financial_destination_id")
            .IsRequired();
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
            .HasOne<CompanyFinancialDestination>()
            .WithMany()
            .HasForeignKey(x => x.FinancialDestinationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.TenantId, x.SupplierPaymentId })
            .HasDatabaseName("ix_supplier_payment_methods_tenant_payment");
        builder
            .HasIndex(x => x.PaymentMethodId)
            .HasDatabaseName("ix_supplier_payment_methods_payment_method");
        builder
            .HasIndex(x => x.FinancialDestinationId)
            .HasDatabaseName("ix_supplier_payment_methods_financial_destination");
    }
}
