using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

public sealed class PurchasePayableConfiguration : IEntityTypeConfiguration<PurchasePayable>
{
    public void Configure(EntityTypeBuilder<PurchasePayable> builder)
    {
        builder.ToTable("purchase_payables");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.PurchaseId).HasColumnName("purchase_id").IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.PaidAmount).HasColumnName("paid_amount").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.TotalRetained).HasColumnName("total_retained").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();

        builder.Ignore(x => x.BalanceDue);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasMany(x => x.Installments).WithOne().HasForeignKey(x => x.PayableId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.CompanyId }).HasDatabaseName("ix_purchase_payables_tenant_company");
        builder.HasIndex(x => x.PurchaseId).IsUnique().HasDatabaseName("uq_purchase_payables_purchase");
    }
}
