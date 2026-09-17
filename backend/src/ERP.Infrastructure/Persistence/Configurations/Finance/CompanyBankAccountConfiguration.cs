using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.MasterData.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Finance;

/// <summary>TREASURY-BANK-ACCOUNTS-01: mapeo EF de <see cref="CompanyBankAccount"/>.</summary>
public sealed class CompanyBankAccountConfiguration : IEntityTypeConfiguration<CompanyBankAccount>
{
    public void Configure(EntityTypeBuilder<CompanyBankAccount> builder)
    {
        builder.ToTable("company_bank_accounts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.BankId).HasColumnName("bank_id").IsRequired();
        builder
            .Property(x => x.AccountType)
            .HasColumnName("account_type")
            .HasConversion<int>()
            .IsRequired();
        builder
            .Property(x => x.AccountNumber)
            .HasColumnName("account_number")
            .HasMaxLength(CompanyBankAccount.AccountNumberMaxLen)
            .IsRequired();
        builder
            .Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(CompanyBankAccount.DisplayNameMaxLen)
            .IsRequired();
        builder
            .Property(x => x.AccountingAccountId)
            .HasColumnName("accounting_account_id")
            .IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // ── Relationships ────────────────────────────────────────────────
        builder
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Bank>()
            .WithMany()
            .HasForeignKey(x => x.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountingAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────────────────
        builder
            .HasIndex(x => new { x.TenantId, x.CompanyId })
            .HasDatabaseName("ix_company_bank_accounts_tenant_company");

        // Único por CompanyId + BankId + AccountType + AccountNumber (regla del ticket).
        builder
            .HasIndex(x => new
            {
                x.TenantId,
                x.CompanyId,
                x.BankId,
                x.AccountType,
                x.AccountNumber,
            })
            .IsUnique()
            .HasDatabaseName("uq_company_bank_accounts_tenant_company_bank_type_number");
    }
}
