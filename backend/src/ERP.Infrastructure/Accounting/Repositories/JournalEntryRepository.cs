using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class JournalEntryRepository : IJournalEntryRepository
{
    private readonly ErpDbContext _context;

    public JournalEntryRepository(ErpDbContext context)
    {
        _context = context;
    }

    public Task<JournalEntry?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default)
        // Include(Lines) — Fase 5.4: ReverseJournalEntryHandler necesita las líneas del asiento
        // original para construir el reverso (JournalEntry.Reverse invierte cada línea); sin este
        // Include, la colección llegaría vacía (sin lazy loading, ver JournalFactory/PostingRule).
        => _context.JournalEntries
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.Id == id)
            .FirstOrDefaultAsync(ct);

    public Task<JournalEntry?> FindByKeyAsync(
        Guid tenantId, Guid companyId, string sourceModule, string factType, Guid sourceEventId, CancellationToken ct = default)
        => _context.JournalEntries
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId
                && x.SourceModule == sourceModule && x.SourceEventType == factType && x.SourceEventId == sourceEventId)
            .FirstOrDefaultAsync(ct);

    public async Task AcquireIdempotencyLockAsync(
        Guid companyId, string sourceModule, Guid sourceEventId, string factType, CancellationToken ct = default)
    {
        // Advisory lock de transacción — serializa concurrentes para la misma clave de
        // idempotencia (CompanyId, SourceModule, SourceEventId, FactType) sin bloquear otras
        // claves. pg_advisory_xact_lock(int4, int4) se libera automáticamente al hacer
        // COMMIT/ROLLBACK de la transacción ambiente — nunca abre ni comitea una transacción
        // propia (la persistencia pertenece exclusivamente a ErpDbContext.SaveChangesAsync).
        var hash1 = StableHash(companyId.ToByteArray().Concat(System.Text.Encoding.UTF8.GetBytes(sourceModule)).ToArray());
        var hash2 = StableHash(sourceEventId.ToByteArray().Concat(System.Text.Encoding.UTF8.GetBytes(factType)).ToArray());
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({hash1}, {hash2})", ct);
    }

    // Hash estable (no depende de HashCode.GetHashCode, no-determinístico en .NET 5+) — mismo
    // algoritmo que DocumentSequenceRepository, duplicado deliberadamente (sin helper
    // compartido: un solo consumidor adicional no justifica extraerlo todavía). Un hash de 32
    // bits puede colisionar en teoría; una colisión solo produce una espera adicional entre dos
    // claves de negocio distintas (falso positivo de contención) — nunca un duplicado, porque la
    // protección final contra duplicados sigue siendo el índice único
    // uq_journal_entries_company_source_event_fact.
    private static int StableHash(byte[] bytes)
    {
        int h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }

    public Task AddAsync(JournalEntry entry, CancellationToken ct = default)
        => _context.JournalEntries.AddAsync(entry, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public async Task<JournalEntryClosureReadiness> GetClosureReadinessAsync(
        Guid tenantId, Guid companyId, Guid accountingPeriodId, CancellationToken ct = default)
    {
        var scoped = _context.JournalEntries
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.AccountingPeriodId == accountingPeriodId);

        // Cada AnyAsync se traduce a "SELECT EXISTS(...)" — nunca materializa JournalEntry
        // completos, ni siquiera para contar (ADR-026 §6.1/§9: cierre es cross-aggregate, pero
        // no debe cargar el período entero para decidir).
        var hasDraftOrNonFinal = await scoped.AnyAsync(
            x => x.Status != JournalEntryStatus.Posted && x.Status != JournalEntryStatus.Reversed, ct);

        var hasWithoutEntryNumber = await scoped.AnyAsync(
            x => x.Status != JournalEntryStatus.Draft && x.EntryNumber == null, ct);

        var hasIncompleteReversals = await scoped.AnyAsync(x =>
            (x.Status == JournalEntryStatus.Reversed && x.ReverseJournalEntryId == null) ||
            (x.OriginalJournalEntryId != null && x.Status != JournalEntryStatus.Posted), ct);

        return new JournalEntryClosureReadiness(hasDraftOrNonFinal, hasWithoutEntryNumber, hasIncompleteReversals);
    }
}
