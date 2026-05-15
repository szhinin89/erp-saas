using ERP.Domain.Modules.Expenses.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class ExpenseDetailConfiguration : IEntityTypeConfiguration<ExpenseDetail>
{
    public void Configure(EntityTypeBuilder<ExpenseDetail> builder)
    {
        builder.ToTable("expense_detail");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ExpenseId).HasColumnName("expense_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.TaxAmount).HasColumnName("tax_amount").HasPrecision(18, 4).HasDefaultValue(0m);
        builder.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue((short)0);

        builder.HasOne(x => x.Expense)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ExpenseId).HasDatabaseName("ix_expense_detail_expense_id");
        builder.HasIndex(x => new { x.ExpenseId, x.SortOrder }).HasDatabaseName("ix_expense_detail_expense_sort");
    }
}
