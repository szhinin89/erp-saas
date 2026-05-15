using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.ValueObjects;

namespace ERP.Infrastructure.Persistence.Configurations;

public class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable("journal_entry_lines");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(l => l.JournalEntryId).HasColumnName("journal_entry_id").IsRequired();
        builder.Property(l => l.AccountId).HasColumnName("account_id").IsRequired();

        builder.OwnsOne(l => l.Debit, debit =>
        {
            debit.Property(m => m.Amount).HasColumnName("debit_amount").HasPrecision(18, 2);
            debit.Property(m => m.Currency).HasColumnName("debit_currency").HasMaxLength(3);
        });

        builder.OwnsOne(l => l.Credit, credit =>
        {
            credit.Property(m => m.Amount).HasColumnName("credit_amount").HasPrecision(18, 2);
            credit.Property(m => m.Currency).HasColumnName("credit_currency").HasMaxLength(3);
        });
    }
}
