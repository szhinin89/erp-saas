using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

public sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable("journal_entry_lines");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.JournalEntryId).HasColumnName("journal_entry_id").IsRequired();
        builder.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);

        // numeric(18,2) — montos (CLAUDE.md, Estándar de Precisión Numérica INMUTABLE).
        builder.Property(x => x.Debit).HasColumnName("debit").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.Credit).HasColumnName("credit").HasColumnType("numeric(18,2)").IsRequired();

        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

        // FK real a Account (a diferencia de PostingRule.DebitAccountId/CreditAccountId, que son
        // configuración sin FK) — JournalEntryLine es el hecho contable ya persistido, no
        // configuración; integridad referencial real importa más aquí (ver diseño Fase 3.5.1).
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.JournalEntryId)
            .HasDatabaseName("ix_journal_entry_lines_journal_entry");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_journal_entry_lines_tenant");
    }
}
