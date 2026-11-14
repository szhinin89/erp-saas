using ERP.Domain.MasterData.Entities;
using ERP.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public sealed class SupplierExpenseSubcategoryAuditConfiguration
    : IEntityTypeConfiguration<SupplierExpenseSubcategoryAudit>
{
    public void Configure(EntityTypeBuilder<SupplierExpenseSubcategoryAudit> builder)
    {
        builder.ConfigureAuditBase("supplier_expense_subcategory_audit");

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
            .HasIndex(x => new
            {
                x.TenantId,
                x.BusinessPartnerId,
                x.OccurredAtUtc,
            })
            .HasDatabaseName("ix_supplier_expense_subcategory_audit_bp_occurred_at");
    }
}
