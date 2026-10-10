using ERP.Domain.Modules.InitialLoad.Entities;

namespace ERP.Domain.Modules.InitialLoad.Interfaces;

/// <summary>IL-7A — estado contable de la apertura por lote (uno por empresa + lote).</summary>
public interface IOpeningBalancePostingRepository
{
    Task<OpeningBalancePosting?> FindByBatchAsync(
        Guid tenantId,
        Guid companyId,
        Guid importBatchId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// IL-7B — serializa la contabilización de un lote con el mismo bloqueo de fila del lote que
    /// usan confirmar/revalidar/cancelar (<c>FOR UPDATE</c> por lote + tenant + empresa). Requiere
    /// una transacción abierta en un proveedor relacional.
    /// </summary>
    Task LockBatchAsync(
        Guid tenantId,
        Guid companyId,
        Guid importBatchId,
        CancellationToken cancellationToken = default
    );

    /// <summary>IL-7C — todos los estados contables de apertura de la empresa, solo lectura (sin tracking).</summary>
    Task<IReadOnlyList<OpeningBalancePosting>> ListByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(OpeningBalancePosting posting, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
