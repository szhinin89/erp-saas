using ERP.Domain.Modules.Cash.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Cash;

public sealed class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("bank_transaction");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.BankStatementId).HasColumnName("bank_statement_id").IsRequired();
        builder.Property(x => x.TransactionDate).HasColumnName("transaction_date").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(BankTransaction.DescriptionMaxLen).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(x => x.TransactionType).HasColumnName("transaction_type").HasMaxLength(BankTransaction.TransactionTypeMaxLen).IsRequired();
        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(BankTransaction.ReferenceMaxLen);
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(BankTransaction.StatusMaxLen).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.BankStatementId, x.TransactionDate })
            .HasDatabaseName("ix_bank_transaction_tenant_statement_date");

        builder.HasOne<ERP.Domain.Modules.Accounting.Entities.JournalEntry>()
            .WithMany()
            .HasForeignKey(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
