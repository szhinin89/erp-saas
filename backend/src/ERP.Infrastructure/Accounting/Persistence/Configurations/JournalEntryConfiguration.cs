using ERP.Domain.Modules.Accounting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Accounting.Persistence.Configurations;

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();

        builder.Property(x => x.EntryDate).HasColumnName("entry_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.AccountingPeriodId).HasColumnName("accounting_period_id").IsRequired();
        builder.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
        builder.Property(x => x.SourceModule).HasColumnName("source_module").HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceEventType).HasColumnName("source_event_type").HasMaxLength(100).IsRequired();

        // SourceEventId es Guid no-nullable en el dominio (JournalEntry.cs) — todo asiento
        // tiene, por diseño, un hecho de origen identificable. No aplica estrategia de índice
        // parcial/filtrado por NULL.
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id").IsRequired();

        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.PostedAtUtc).HasColumnName("posted_at_utc");
        builder.Property(x => x.EntryNumber).HasColumnName("entry_number");
        builder.Property(x => x.OriginalJournalEntryId).HasColumnName("original_journal_entry_id");
        builder.Property(x => x.ReverseJournalEntryId).HasColumnName("reverse_journal_entry_id");
        builder.Property(x => x.ReversedAtUtc).HasColumnName("reversed_at_utc");
        builder.Property(x => x.ReverseReason).HasColumnName("reverse_reason").HasMaxLength(500);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        // Intra-módulo (misma fase, ambas tablas nacen juntas) — FK física, sin navegación de
        // dominio (JournalEntry no referencia el aggregate AccountingPeriod, solo su Id),
        // mismo patrón ya usado por PricingRuleConfiguration → PriceList.
        builder.HasOne<AccountingPeriod>()
            .WithMany()
            .HasForeignKey(x => x.AccountingPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        // Clave de idempotencia del Posting Engine (Fase 3.1, ADR-026 §8): un hecho contable
        // (CompanyId, SourceModule, FactType/SourceEventType, SourceEventId) produce como máximo
        // un JournalEntry. PostingIdempotencyGuard consulta esta clave antes de contabilizar;
        // en carrera, la violación UNIQUE se traduce vía IDatabaseExceptionTranslator y la
        // segunda ejecución retorna AlreadyProcessed en vez de fallar.
        builder.HasIndex(x => new { x.CompanyId, x.SourceModule, x.SourceEventId, x.SourceEventType })
            .IsUnique()
            .HasDatabaseName("uq_journal_entries_company_source_event_fact");

        // Numeración definitiva (Fase 5.3, ADR-026 §7): un mismo (CompanyId, FiscalYear) nunca
        // repite EntryNumber. PostgreSQL trata cada NULL como distinto en un índice único, por lo
        // que los asientos aún en Draft (EntryNumber nulo) nunca colisionan entre sí — la
        // restricción solo aplica una vez el asiento queda Posted.
        builder.HasIndex(x => new { x.CompanyId, x.FiscalYear, x.EntryNumber })
            .IsUnique()
            .HasDatabaseName("uq_journal_entries_company_fiscal_year_entry_number");

        // Reverso contable (Fase 5.4, ADR-026 §9): auto-FK sin navegación de dominio, mismo
        // criterio que AccountingPeriodId. Único por OriginalJournalEntryId — un asiento original
        // no puede tener más de un asiento de reverso (JournalEntry.Reverse ya lo impide en
        // memoria vía el chequeo de Status==Posted; este índice es la garantía final de BD bajo
        // concurrencia real). PostgreSQL trata cada NULL como distinto, por lo que los asientos
        // que no son reverso de nada (la inmensa mayoría) nunca colisionan entre sí.
        builder.HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(x => x.OriginalJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(x => x.ReverseJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OriginalJournalEntryId)
            .IsUnique()
            .HasDatabaseName("uq_journal_entries_original_journal_entry_id");

        // JournalEntryLine (partida doble) — persistencia agregada en Fase 3.5.4. FK configurada
        // desde el lado padre (mismo patrón que PurchaseInvoice→PurchaseInvoiceDetail): Cascade
        // porque una línea no tiene sentido de existir sin su asiento — borrar el asiento borra
        // sus líneas (append-only en la práctica, ya que JournalEntry no expone Delete()).
        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
