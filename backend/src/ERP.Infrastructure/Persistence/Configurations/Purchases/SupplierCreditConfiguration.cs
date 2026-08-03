using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Purchases;

/// <summary>Mapeo EF de <see cref="SupplierCredit"/> — diseño P0-02 §7.4, Fase 2.</summary>
public sealed class SupplierCreditConfiguration : IEntityTypeConfiguration<SupplierCredit>
{
    public void Configure(EntityTypeBuilder<SupplierCredit> builder)
    {
        builder.ToTable("supplier_credits");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        // Branch Ownership Rule (§5.2) — heredado literalmente de PurchaseReturn.BranchId.
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();

        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").IsRequired();
        builder
            .Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .IsRequired();
        builder
            .Property(x => x.SourcePurchaseReturnId)
            .HasColumnName("source_purchase_return_id")
            .IsRequired();
        builder
            .Property(x => x.OriginalAmount)
            .HasColumnName("original_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.AvailableAmount)
            .HasColumnName("available_amount")
            .HasColumnType("numeric(18,2)")
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

        // ── Computed properties (NOT persisted) ─────────────────────
        builder.Ignore(x => x.IsOpen);

        // ── Relationships ────────────────────────────────────────────
        builder
            .HasMany(x => x.Movements)
            .WithOne()
            .HasForeignKey(x => x.SupplierCreditId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<PurchaseReturn>()
            .WithMany()
            .HasForeignKey(x => x.SourcePurchaseReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_supplier_credits_tenant_company");

        builder
            .HasIndex(x => new { x.TenantId, x.SupplierId })
            .HasDatabaseName("ix_supplier_credits_tenant_supplier");

        builder
            .HasIndex(x => new { x.TenantId, x.SourcePurchaseReturnId })
            .IsUnique()
            .HasDatabaseName("uq_supplier_credits_tenant_source_purchase_return");
    }
}
