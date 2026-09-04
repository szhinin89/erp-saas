using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class JournalEntrySequenceRepository : IJournalEntrySequenceRepository
{
    private readonly ErpDbContext _context;

    public JournalEntrySequenceRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<int> ReserveNextNumberAsync(
        Guid tenantId,
        Guid companyId,
        int fiscalYear,
        CancellationToken ct = default
    )
    {
        // Advisory lock de transacción ambiente — serializa concurrentes para el mismo
        // (CompanyId, FiscalYear) sin bloquear otras empresas/ejercicios. Se libera
        // automáticamente al COMMIT/ROLLBACK de la transacción externa que ya abre
        // ErpDbContext.SaveChangesAsync — mismo patrón que
        // JournalEntryRepository.AcquireIdempotencyLockAsync (ADR-026 §8).
        var companyHash = StableHash(companyId.ToByteArray());
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({companyHash}, {fiscalYear})",
            ct
        );

        // BUG REAL (RETENTIONS-EXPENSES-E2E-QA-01G): antes de esta corrección, esta línea consultaba
        // SIEMPRE la base de datos directamente. Cuando dos hechos contables se postean dentro de
        // la MISMA transacción ambiente/SaveChangesAsync (p. ej. ConfirmExpenseDocumentHandler con
        // retención: ExpenseDocumentConfirmedEvent + RetentionDocumentIssuedEvent, ambos publicados
        // desde el mismo SaveChangesAsync — ver RETENTIONS-MODULE-DESIGN-01.md § "Flujo desde
        // Gastos"), la primera llamada agregaba una JournalEntrySequence nueva vía AddAsync SIN
        // flushear (el comentario de tipo de JournalEntrySequence.NextNumber() ya documentaba que
        // debía reutilizarse "en la misma transacción ambiente" — esto nunca funcionó). La segunda
        // llamada, en la misma transacción todavía sin flush, no veía esa fila recién agregada (una
        // consulta EF Core no reincorpora entidades Added-sin-guardar) y creaba OTRA
        // JournalEntrySequence nueva, también arrancando en 1 — dos JournalEntry con
        // (company_id, fiscal_year, entry_number)=(_, _, 1) violaban
        // uq_journal_entries_company_fiscal_year_entry_number al hacer flush, abortando SIEMPRE la
        // confirmación completa de cualquier gasto con retención (reproducido end-to-end contra
        // Postgres real). Se corrige revisando primero el ChangeTracker local (patrón estándar de
        // EF Core para este caso) antes de ir a la base de datos — el advisory lock de arriba sigue
        // garantizando exclusión mutua entre transacciones concurrentes distintas.
        var sequence =
            _context
                .ChangeTracker.Entries<JournalEntrySequence>()
                .Select(e => e.Entity)
                .FirstOrDefault(s => s.CompanyId == companyId && s.FiscalYear == fiscalYear)
            ?? await _context.JournalEntrySequences.FirstOrDefaultAsync(
                s => s.CompanyId == companyId && s.FiscalYear == fiscalYear,
                ct
            );

        if (sequence is null)
        {
            sequence = JournalEntrySequence.Create(tenantId, companyId, fiscalYear);
            await _context.JournalEntrySequences.AddAsync(sequence, ct);
        }

        return sequence.NextNumber();
    }

    // Hash estable (no depende de HashCode.GetHashCode, no-determinístico en .NET 5+) — mismo
    // algoritmo que DocumentSequenceRepository/JournalEntryRepository.
    private static int StableHash(byte[] bytes)
    {
        int h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }
}
