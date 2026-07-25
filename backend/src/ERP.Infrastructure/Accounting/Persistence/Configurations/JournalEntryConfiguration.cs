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
        builder.Property(x => x.SourceModule).HasColumnName("source_module").HasMaxLength(50).IsRequired();
        builder.Property(x => x.SourceEventType).HasColumnName("source_event_type").HasMaxLength(100).IsRequired();

        // SourceEventId es Guid no-nullable en el dominio (JournalEntry.cs) — todo asiento
        // tiene, por diseño, un hecho de origen identificable. No aplica estrategia de índice
        // parcial/filtrado por NULL.
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id").IsRequired();

        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();

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
