using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

/// <summary>
/// Configuración EF Core para SupplierExpenseSubcategory — EXPENSE-SUPPLIER-SUBCATEGORIES-01.
///
/// El índice único NO lleva filtro por is_active: la baja es lógica, así que una asignación
/// retirada conserva su fila y se reactiva (nunca se inserta otra).
///
/// QUERY FILTER: ICompanyScopedEntity + ITenantScopedEntity → filtro estricto fail-closed por tenant
/// y empresa (EnterpriseQueryFilterConfigurator.BuildStrictCompanyScopedFilter).
/// </summary>
public sealed class SupplierExpenseSubcategoryConfiguration
    : IEntityTypeConfiguration<SupplierExpenseSubcategory>
{
    public void Configure(EntityTypeBuilder<SupplierExpenseSubcategory> builder)
    {
        builder.ToTable("master_supplier_expense_subcategories");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder
            .Property(x => x.BusinessPartnerId)
            .HasColumnName("business_partner_id")
            .IsRequired();
        builder
            .Property(x => x.ExpenseSubcategoryId)
            .HasColumnName("expense_subcategory_id")
            .IsRequired();
        builder
            .Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        // ── Audit ────────────────────────────────────────────────────────────
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // ── FKs (Restrict: nunca borrado en cascada) ──────────────────────────
        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_bpes_company");
        builder
            .HasOne<BusinessPartner>()
            .WithMany()
            .HasForeignKey(x => x.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_bpes_business_partner");
        builder
            .HasOne<ExpenseCategoryNode>()
            .WithMany()
            .HasForeignKey(x => x.ExpenseSubcategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_bpes_expense_subcategory");

        // ── Índices ───────────────────────────────────────────────────────────
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.BusinessPartnerId,
                x.ExpenseSubcategoryId,
            })
            .IsUnique()
            .HasDatabaseName("uq_bpes_company_bp_subcategory");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.BusinessPartnerId,
                x.IsActive,
            })
            .HasDatabaseName("ix_bpes_company_bp_active");
    }
}
