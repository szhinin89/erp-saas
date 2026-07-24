using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Caja;

/// <summary>
/// Unicidad de <see cref="CashRegister.Code"/>: por sucursal (Tenant+Branch), no por empresa.
/// Justificación (ADR — Rediseño del módulo de Caja): dos sucursales de la misma empresa pueden
/// reutilizar el mismo código de caja (p. ej. "CAJA-01" en cada local de una cadena) sin colisión,
/// porque cada sucursal administra su propio set de cajas físicas de forma independiente.
/// </summary>
public sealed class CashRegisterConfiguration : IEntityTypeConfiguration<CashRegister>
{
    public void Configure(EntityTypeBuilder<CashRegister> builder)
    {
        builder.ToTable("cash_registers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.EmissionPointId).HasColumnName("emission_point_id");
        builder.Property(x => x.DefaultWarehouseId).HasColumnName("default_warehouse_id");
        builder.Property(x => x.DefaultCustomerId).HasColumnName("default_customer_id");

        builder.Property(x => x.Code).HasColumnName("code")
            .HasMaxLength(CashRegister.CodeMaxLen).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name")
            .HasMaxLength(CashRegister.NameMaxLen).IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes")
            .HasMaxLength(CashRegister.NotesMaxLen);

        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.IsSystemSeeded).HasColumnName("is_system_seeded").IsRequired().HasDefaultValue(false);

        // ── Auditoría ───────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── Relationships ───────────────────────────────────────────
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EmissionPoint)
            .WithMany()
            .HasForeignKey(x => x.EmissionPointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DefaultWarehouse)
            .WithMany()
            .HasForeignKey(x => x.DefaultWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DefaultCustomer)
            .WithMany()
            .HasForeignKey(x => x.DefaultCustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ─────────────────────────────────────────────────
        builder.HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_cash_registers_tenant_company");

        builder.HasIndex(x => new { x.TenantId, x.BranchId })
            .HasDatabaseName("ix_cash_registers_tenant_branch");

        builder.HasIndex(x => x.EmissionPointId)
            .HasDatabaseName("ix_cash_registers_emission_point");

        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.Code })
            .IsUnique()
            .HasDatabaseName("uq_cash_registers_tenant_branch_code");
    }
}
