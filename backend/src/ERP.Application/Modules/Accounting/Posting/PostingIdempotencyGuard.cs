using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>Primer paso del pipeline (ADR-026 §8) — un hecho contable ya contabilizado nunca reingresa al pipeline.</summary>
internal sealed class PostingIdempotencyGuard
{
    private readonly IJournalEntryRepository _journalEntryRepository;

    public PostingIdempotencyGuard(IJournalEntryRepository journalEntryRepository)
    {
        _journalEntryRepository = journalEntryRepository;
    }

    public async Task<JournalEntry?> FindExistingAsync(PostingFact fact, CancellationToken ct)
    {
        // El lock se adquiere siempre antes de la lectura — sin él, dos transacciones
        // concurrentes podrían ver "no existe" al mismo tiempo y competir por crear el mismo
        // JournalEntry (ver ADR-026 §8, Fase 3.3.2/3.3.3).
        await _journalEntryRepository.AcquireIdempotencyLockAsync(
            fact.CompanyId, fact.SourceModule, fact.SourceEventId, fact.FactType, ct);

        return await _journalEntryRepository.FindByKeyAsync(
            fact.TenantId, fact.CompanyId, fact.SourceModule, fact.FactType, fact.SourceEventId, ct);
    }
}
