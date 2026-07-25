using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Interfaces;

public interface IJournalEntryRepository
{
    Task<JournalEntry?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default);

    /// <summary>Búsqueda por la clave de idempotencia del Posting Engine — ver uq_journal_entries_company_source_event_fact.</summary>
    Task<JournalEntry?> FindByKeyAsync(
        Guid tenantId, Guid companyId, string sourceModule, string factType, Guid sourceEventId, CancellationToken ct = default);

    /// <summary>
    /// Protege la clave natural de idempotencia del Posting Engine (CompanyId, SourceModule,
    /// SourceEventId, FactType) mediante un advisory lock transaccional de PostgreSQL — debe
    /// ejecutarse siempre antes de <see cref="FindByKeyAsync"/>. Utiliza la transacción ambiente
    /// ya existente (nunca abre una transacción propia); el lock se libera automáticamente al
    /// hacer COMMIT o ROLLBACK de esa transacción.
    /// </summary>
    Task AcquireIdempotencyLockAsync(
        Guid companyId, string sourceModule, Guid sourceEventId, string factType, CancellationToken ct = default);

    Task AddAsync(JournalEntry entry, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
