using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>
/// PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — FKs compuestas con tenant_id/purchase_invoice_id: un
/// origen o destino nunca puede pertenecer a otra compra ni a otro tenant.
/// </summary>
public sealed class PurchaseCostDistributionConfiguration : IEntityTypeConfiguration<PurchaseCostDistribution>
{
    public void Configure(EntityTypeBuilder<PurchaseCostDistribution> builder)
    {
        builder.ToTable("purchase_cost_distributions", t =>
        {
            t.HasCheckConstraint("ck_purchase_cost_distribution_amount", "amount > 0 AND revision > 0");
            t.HasCheckConstraint("ck_purchase_cost_distribution_type", "cost_type IN (1, 2)");
        });
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.InvoiceId).HasColumnName("purchase_invoice_id");
        builder.Property(d => d.CostType).HasColumnName("cost_type").HasConversion<int>();
        builder.Property(d => d.SourceInvoiceDetailId).HasColumnName("source_invoice_detail_id");
        builder.Property(d => d.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(d => d.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.Ignore(d => d.IsExternal);
        builder.HasAlternateKey(d => new { d.TenantId, d.InvoiceId, d.Id });
        builder.HasOne<PurchaseInvoice>().WithMany(i => i.CostDistributions)
            .HasForeignKey(d => new { d.TenantId, d.InvoiceId }).HasPrincipalKey(i => new { i.TenantId, i.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PurchaseInvoiceDetail>().WithMany()
            .HasForeignKey(d => new { d.TenantId, d.InvoiceId, d.SourceInvoiceDetailId })
            .HasPrincipalKey(l => new { l.TenantId, l.InvoiceId, l.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.TenantId, d.InvoiceId, d.SourceInvoiceDetailId }).IsUnique()
            .HasFilter("source_invoice_detail_id IS NOT NULL");
        builder.HasMany(d => d.Allocations).WithOne(a => a.Distribution)
            .HasForeignKey(a => new { a.TenantId, a.InvoiceId, a.DistributionId })
            .HasPrincipalKey(d => new { d.TenantId, d.InvoiceId, d.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PurchaseCostDistributionAllocationConfiguration : IEntityTypeConfiguration<PurchaseCostDistributionAllocation>
{
    public void Configure(EntityTypeBuilder<PurchaseCostDistributionAllocation> builder)
    {
        builder.ToTable("purchase_cost_distribution_allocations", t =>
            t.HasCheckConstraint("ck_purchase_cost_allocation_amount", "amount >= 0"));
        builder.HasKey(a => new { a.DistributionId, a.TargetInvoiceDetailId });
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.InvoiceId).HasColumnName("purchase_invoice_id");
        builder.Property(a => a.DistributionId).HasColumnName("distribution_id");
        builder.Property(a => a.TargetInvoiceDetailId).HasColumnName("target_invoice_detail_id");
        builder.Property(a => a.Amount).HasColumnName("amount").HasColumnType("numeric(18,6)");
        builder.HasOne<PurchaseInvoiceDetail>().WithMany()
            .HasForeignKey(a => new { a.TenantId, a.InvoiceId, a.TargetInvoiceDetailId })
            .HasPrincipalKey(l => new { l.TenantId, l.InvoiceId, l.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
