using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Accounting;

public sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("expense_category");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(ExpenseCategory.CategoryMaxLen).IsRequired();
        builder.Property(e => e.ExpenseAccountId).HasColumnName("expense_account_id").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.SubscriberId, e.Category }).IsUnique().HasDatabaseName("uq_expense_category_subscriber_cat");

        builder.HasOne<Account>().WithMany().HasForeignKey(e => e.ExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
