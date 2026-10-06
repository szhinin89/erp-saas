using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class SaleCostAllocationConfiguration : IEntityTypeConfiguration<SaleCostAllocation>
{
    public void Configure(EntityTypeBuilder<SaleCostAllocation> b)
    {
        b.ToTable("sale_cost_allocations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ObligationId, x.OriginMovementId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceId });
        b.Property(x => x.Kind).HasMaxLength(20);
        b.Property(x => x.PendingQuantity).HasColumnType("numeric(20,6)");
        b.Property(x => x.ResolvedQuantity).HasColumnType("numeric(20,6)");
        b.Property(x => x.PreviousUnitCost).HasColumnType("numeric(22,10)");
        b.Property(x => x.ActualUnitCost).HasColumnType("numeric(22,10)");
        b.Property(x => x.CogsAdjustment).HasColumnType("numeric(22,10)");
        b.HasOne<SaleCostObligation>().WithMany().HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StockMovement>().WithMany().HasForeignKey(x => x.OriginMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
