using ERP.Domain.Modules.Cash.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("bank_account");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(BankAccount.NameMaxLen).IsRequired();
        builder.Property(x => x.AccountNumber).HasColumnName("account_number").HasMaxLength(BankAccount.AccountNumberMaxLen).IsRequired();
        builder.Property(x => x.AccountType).HasColumnName("account_type").HasMaxLength(BankAccount.AccountTypeMaxLen).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(BankAccount.CurrencyMaxLen).IsRequired();
        builder.Property(x => x.InitialBalance).HasColumnName("initial_balance").HasPrecision(18, 2);
        builder.Property(x => x.CurrentBalance).HasColumnName("current_balance").HasPrecision(18, 2);
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.LedgerAccountId).HasColumnName("ledger_account_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => new { x.TenantId, x.AccountNumber })
            .IsUnique()
            .HasDatabaseName("uq_bank_account_tenant_number");

        builder.HasOne<ERP.Domain.Modules.Accounting.Entities.Account>()
            .WithMany()
            .HasForeignKey(x => x.LedgerAccountId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
