using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Domain.Modules.InitialLoad.Interfaces;

public interface IImportBatchRepository
{
    Task<ImportBatch?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// IL-8E — <c>FOR UPDATE</c> de la empresa (requiere transacción). Se toma ANTES del bloqueo del
    /// lote (orden fijo empresa → lote, el mismo de la apertura) para serializar la confirmación de
    /// un lote de saldos con el cierre definitivo de la Carga Inicial.
    /// </summary>
    Task LockCompanyAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);

    // Requires an active transaction; reloads state after obtaining the scoped row lock.
    Task<ImportBatch?> GetByIdForUpdateAsync(Guid id, Guid tenantId, Guid companyId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ImportBatch> Batches, int TotalCount)> GetPageAsync(
        Guid tenantId,
        Guid companyId,
        ImportType? importType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    /// <summary>IL-7C — lotes de los tipos y estados indicados, solo lectura (sin tracking).</summary>
    Task<IReadOnlyList<ImportBatch>> ListAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyCollection<ImportType> importTypes,
        IReadOnlyCollection<ImportStatus> statuses,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(ImportBatch batch, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
