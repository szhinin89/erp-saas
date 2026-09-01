using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Expenses;

public sealed class ExpenseCategoryNodeConfiguration
    : IEntityTypeConfiguration<ExpenseCategoryNode>
{
    public void Configure(EntityTypeBuilder<ExpenseCategoryNode> builder)
    {
        builder.ToTable(
            "expense_category_nodes",
            t =>
                t.HasCheckConstraint(
                    "chk_expense_category_nodes_hierarchy",
                    "(\"level\" = 0 AND \"parent_id\" IS NULL AND \"accounting_account_id\" IS NULL) "
                        + "OR (\"level\" = 1 AND \"parent_id\" IS NOT NULL AND \"accounting_account_id\" IS NULL) "
                        + "OR (\"level\" = 2 AND \"parent_id\" IS NOT NULL AND \"accounting_account_id\" IS NOT NULL)"
                )
        );

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ParentId).HasColumnName("parent_id");
        builder
            .Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(ExpenseCategoryNode.CodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(ExpenseCategoryNode.NameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(ExpenseCategoryNode.DescriptionMaxLen);
        builder.Property(x => x.Level).HasColumnName("level").HasConversion<int>().IsRequired();
        builder.Property(x => x.AccountingAccountId).HasColumnName("accounting_account_id");
        builder.Property(x => x.IsDeductible).HasColumnName("is_deductible").IsRequired();
        builder.Property(x => x.RequiresInvoice).HasColumnName("requires_invoice").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<ExpenseCategoryNode>()
            .WithMany()
            .HasForeignKey(x => x.ParentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountingAccountId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_expense_category_nodes_tenant_company");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.Level,
                x.Code,
            })
            .IsUnique()
            .HasFilter("\"parent_id\" IS NULL")
            .HasDatabaseName("uq_expense_category_nodes_root_code");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.Level,
                x.Name,
            })
            .IsUnique()
            .HasFilter("\"parent_id\" IS NULL")
            .HasDatabaseName("uq_expense_category_nodes_root_name");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.ParentId,
                x.Level,
                x.Code,
            })
            .IsUnique()
            .HasFilter("\"parent_id\" IS NOT NULL")
            .HasDatabaseName("uq_expense_category_nodes_parent_code");

        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.ParentId,
                x.Level,
                x.Name,
            })
            .IsUnique()
            .HasFilter("\"parent_id\" IS NOT NULL")
            .HasDatabaseName("uq_expense_category_nodes_parent_name");

        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId, x.IsActive })
            .HasDatabaseName("ix_expense_category_nodes_tenant_company_active");
    }
}
