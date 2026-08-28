using ERP.Domain.Modules.Payables.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Payables;

public sealed class SupplierPaymentAllocationLineConfiguration
    : IEntityTypeConfiguration<SupplierPaymentAllocationLine>
{
    public void Configure(EntityTypeBuilder<SupplierPaymentAllocationLine> builder)
    {
        builder.ToTable("supplier_payment_allocations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SupplierPaymentId).HasColumnName("supplier_payment_id").IsRequired();
        builder
            .Property(x => x.SupplierPaymentMethodLineId)
            .HasColumnName("supplier_payment_method_line_id")
            .IsRequired();
        builder
            .Property(x => x.SupplierPaymentApplicationLineId)
            .HasColumnName("supplier_payment_application_line_id")
            .IsRequired();
        builder
            .Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .HasOne<SupplierPaymentMethodLine>()
            .WithMany()
            .HasForeignKey(x => x.SupplierPaymentMethodLineId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<SupplierPaymentApplicationLine>()
            .WithMany()
            .HasForeignKey(x => x.SupplierPaymentApplicationLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(x => new { x.TenantId, x.SupplierPaymentId })
            .HasDatabaseName("ix_supplier_payment_allocations_tenant_payment");
        builder
            .HasIndex(x => x.SupplierPaymentMethodLineId)
            .HasDatabaseName("ix_supplier_payment_allocations_method_line");
        builder
            .HasIndex(x => x.SupplierPaymentApplicationLineId)
            .HasDatabaseName("ix_supplier_payment_allocations_application_line");
    }
}
