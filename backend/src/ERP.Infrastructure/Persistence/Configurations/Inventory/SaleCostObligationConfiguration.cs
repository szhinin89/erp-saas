using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class SaleCostObligationConfiguration : IEntityTypeConfiguration<SaleCostObligation>
{
    public void Configure(EntityTypeBuilder<SaleCostObligation> b)
    {
        b.ToTable("sale_cost_obligations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.SaleMovementId).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId, x.WarehouseId });
        b.HasIndex(x => new { x.CompanyId, x.InvoiceId, x.InvoiceLineId }).IsUnique();
        b.Property(x => x.ProvisionalUnitCost).HasColumnType("numeric(22,10)");
        b.Property(x => x.PendingQuantity).HasColumnType("numeric(20,6)");
        b.Property(x => x.ResolvedQuantity).HasColumnType("numeric(20,6)");
        b.Property(x => x.ReturnedQuantity).HasColumnType("numeric(20,6)");
        b.Property(x => x.ResolvedCost).HasColumnType("numeric(22,10)");
        b.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
        b.HasOne<StockMovement>().WithMany().HasForeignKey(x => x.SaleMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
