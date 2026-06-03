using ERP.Domain.Modules.Cash.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class PettyCashExpenseConfiguration : IEntityTypeConfiguration<PettyCashExpense>
{
    public void Configure(EntityTypeBuilder<PettyCashExpense> builder)
    {
        builder.ToTable("petty_cash_expense");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.PettyCashId).HasColumnName("petty_cash_id").IsRequired();
        builder.Property(x => x.ExpenseDate).HasColumnName("expense_date").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(PettyCashExpense.DescriptionMaxLen).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(x => x.VoucherType).HasColumnName("voucher_type").HasMaxLength(PettyCashExpense.VoucherTypeMaxLen).IsRequired();
        builder.Property(x => x.VoucherNumber).HasColumnName("voucher_number").HasMaxLength(PettyCashExpense.VoucherNumberMaxLen);
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<PettyCash>()
            .WithMany()
            .HasForeignKey(x => x.PettyCashId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ERP.Domain.Modules.Accounting.Entities.JournalEntry>()
            .WithMany()
            .HasForeignKey(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
