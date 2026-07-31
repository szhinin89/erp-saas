using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Sales;

public sealed class SalesReturnRefundAllocationConfiguration
    : IEntityTypeConfiguration<SalesReturnRefundAllocation>
{
    public void Configure(EntityTypeBuilder<SalesReturnRefundAllocation> builder)
    {
        builder.ToTable("sales_return_refund_allocations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ReturnId).HasColumnName("return_id").IsRequired();

        builder.Property(x => x.Method).HasColumnName("method").HasConversion<int>().IsRequired();
        builder
            .Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .HasIndex(x => new { x.TenantId, x.ReturnId })
            .HasDatabaseName("ix_sales_return_refund_allocations_tenant_return");
    }
}
