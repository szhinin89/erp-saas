using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Expenses;

public sealed class ExpenseLineConfiguration : IEntityTypeConfiguration<ExpenseLine>
{
    public void Configure(EntityTypeBuilder<ExpenseLine> builder)
    {
        builder.ToTable("expense_lines");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.ExpenseDocumentId).HasColumnName("expense_document_id").IsRequired();
        builder
            .Property(x => x.ExpenseSubcategoryId)
            .HasColumnName("expense_subcategory_id")
            .IsRequired();
        builder
            .Property(x => x.SnapshotAccountingAccountId)
            .HasColumnName("snapshot_accounting_account_id")
            .IsRequired();
        builder
            .Property(x => x.SnapshotAccountingAccountCode)
            .HasColumnName("snapshot_accounting_account_code")
            .HasMaxLength(ExpenseLine.AccountCodeMaxLen);
        builder
            .Property(x => x.SnapshotAccountingAccountName)
            .HasColumnName("snapshot_accounting_account_name")
            .HasMaxLength(ExpenseLine.AccountNameMaxLen);
        builder
            .Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(ExpenseLine.DescriptionMaxLen)
            .IsRequired();
        builder
            .Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("numeric(18,4)")
            .IsRequired();
        builder
            .Property(x => x.UnitAmount)
            .HasColumnName("unit_amount")
            .HasColumnType("numeric(18,6)")
            .IsRequired();
        builder
            .Property(x => x.DiscountPct)
            .HasColumnName("discount_pct")
            .HasColumnType("numeric(5,2)")
            .IsRequired();
        builder
            .Property(x => x.DiscountAmount)
            .HasColumnName("discount_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.VatCode)
            .HasColumnName("vat_code")
            .HasMaxLength(ExpenseLine.VatCodeMaxLen)
            .IsRequired();
        builder
            .Property(x => x.VatRate)
            .HasColumnName("vat_rate")
            .HasColumnType("numeric(5,2)")
            .IsRequired();
        builder
            .Property(x => x.VatAmount)
            .HasColumnName("vat_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder
            .Property(x => x.SnapshotVatName)
            .HasColumnName("snapshot_vat_name")
            .HasMaxLength(ExpenseLine.VatNameMaxLen);
        builder
            .Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(ExpenseLine.NotesMaxLen);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.Ignore(x => x.LineSubtotal);
        builder.Ignore(x => x.TaxableBase);
        builder.Ignore(x => x.TaxInclusiveTotal);

        builder
            .HasOne<ExpenseCategoryNode>()
            .WithMany()
            .HasForeignKey(x => x.ExpenseSubcategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.SnapshotAccountingAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => x.ExpenseDocumentId)
            .HasDatabaseName("ix_expense_lines_document");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_expense_lines_tenant");

        builder
            .HasIndex(x => new { x.TenantId, x.ExpenseSubcategoryId })
            .HasDatabaseName("ix_expense_lines_tenant_subcategory");
    }
}
