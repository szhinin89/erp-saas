using ERP.Domain.Modules.Cash.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class BankStatementConfiguration : IEntityTypeConfiguration<BankStatement>
{
    public void Configure(EntityTypeBuilder<BankStatement> builder)
    {
        builder.ToTable("bank_statement");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.BankAccountId).HasColumnName("bank_account_id").IsRequired();
        builder.Property(x => x.PeriodFrom).HasColumnName("period_from").IsRequired();
        builder.Property(x => x.PeriodTo).HasColumnName("period_to").IsRequired();
        builder.Property(x => x.OpeningBalance).HasColumnName("opening_balance").HasPrecision(18, 2);
        builder.Property(x => x.ClosingBalance).HasColumnName("closing_balance").HasPrecision(18, 2);
        builder.Property(x => x.LoadedAt).HasColumnName("loaded_at").IsRequired();
        builder.Property(x => x.IsReconciled).HasColumnName("is_reconciled").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Transactions)
            .WithOne()
            .HasForeignKey(m => m.BankStatementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SubscriberId, x.BankAccountId, x.PeriodFrom, x.PeriodTo })
            .HasDatabaseName("ix_bank_statement_subscriber_account_period");
    }
}
