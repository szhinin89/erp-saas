using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

public sealed class JournalEntrySequenceConfiguration : IEntityTypeConfiguration<JournalEntrySequence>
{
    public void Configure(EntityTypeBuilder<JournalEntrySequence> builder)
    {
        builder.ToTable("journal_entry_sequences", t =>
            t.HasCheckConstraint("chk_journal_entry_seq_non_negative", "last_number >= 0"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
        builder.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Una fila por (CompanyId, FiscalYear) — el advisory lock de
        // JournalEntrySequenceRepository.ReserveNextNumberAsync serializa la creación on-demand
        // de esta fila; este índice es la garantía final a nivel de BD.
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.FiscalYear })
            .IsUnique()
            .HasDatabaseName("uq_journal_entry_sequences_company_fiscal_year");
    }
}
