using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>ACCOUNTING-12A — mapeo EF de <see cref="InventoryAdjustmentReasonAccount"/>.</summary>
public sealed class InventoryAdjustmentReasonAccountConfiguration
    : IEntityTypeConfiguration<InventoryAdjustmentReasonAccount>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustmentReasonAccount> builder)
    {
        builder.ToTable("inventory_adjustment_reason_accounts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.InventoryAdjustmentReasonId)
            .HasColumnName("inventory_adjustment_reason_id")
            .IsRequired();
        builder
            .Property(x => x.AccountingAccountId)
            .HasColumnName("accounting_account_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<InventoryAdjustmentReason>()
            .WithMany()
            .HasForeignKey(x => x.InventoryAdjustmentReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountingAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.CompanyId, x.InventoryAdjustmentReasonId })
            .IsUnique()
            .HasDatabaseName("uq_inventory_adjustment_reason_accounts_company_reason");
    }
}
