using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Accounting.Repositories;

public sealed class JournalEntrySequenceRepository : IJournalEntrySequenceRepository
{
    private readonly ErpDbContext _context;

    public JournalEntrySequenceRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<int> ReserveNextNumberAsync(
        Guid tenantId, Guid companyId, int fiscalYear, CancellationToken ct = default)
    {
        // Advisory lock de transacción ambiente — serializa concurrentes para el mismo
        // (CompanyId, FiscalYear) sin bloquear otras empresas/ejercicios. Se libera
        // automáticamente al COMMIT/ROLLBACK de la transacción externa que ya abre
        // ErpDbContext.SaveChangesAsync — mismo patrón que
        // JournalEntryRepository.AcquireIdempotencyLockAsync (ADR-026 §8).
        var companyHash = StableHash(companyId.ToByteArray());
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({companyHash}, {fiscalYear})", ct);

        var sequence = await _context.JournalEntrySequences
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.FiscalYear == fiscalYear, ct);

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
