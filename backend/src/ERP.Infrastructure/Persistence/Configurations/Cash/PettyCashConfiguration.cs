using ERP.Domain.Modules.Cash.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class PettyCashConfiguration : IEntityTypeConfiguration<PettyCash>
{
    public void Configure(EntityTypeBuilder<PettyCash> builder)
    {
        builder.ToTable("petty_cash");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(PettyCash.NameMaxLen).IsRequired();
        builder.Property(x => x.AssignedBalance).HasColumnName("assigned_balance").HasPrecision(18, 2);
        builder.Property(x => x.CurrentBalance).HasColumnName("current_balance").HasPrecision(18, 2);
        builder.Property(x => x.ReplenishBankAccountId).HasColumnName("replenish_bank_account_id");
        builder.Property(x => x.LedgerAccountId).HasColumnName("ledger_account_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(x => x.ReplenishBankAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ERP.Domain.Modules.Accounting.Entities.Account>()
            .WithMany()
            .HasForeignKey(x => x.LedgerAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.SubscriberId, x.Name })
            .IsUnique()
            .HasDatabaseName("uq_petty_cash_subscriber_name");
    }
}
