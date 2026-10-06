using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

public sealed class InventoryCostPostingConfiguration : IEntityTypeConfiguration<InventoryCostPosting>
{
    public void Configure(EntityTypeBuilder<InventoryCostPosting> b)
    {
        b.ToTable("inventory_cost_postings");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.CompanyId, x.SourceEventId, x.FactType }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CompanyId, x.InvoiceId });
        b.Property(x => x.Amount).HasColumnType("numeric(22,10)");
        b.Property(x => x.EntryDate).HasColumnType("date");
        b.Property(x => x.FactType).HasMaxLength(60);
        b.Property(x => x.Kind).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.ErrorCode).HasMaxLength(100);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        b.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
    }
}
