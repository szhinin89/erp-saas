using ERP.Domain.Modules.InitialLoad.Entities;

namespace ERP.Domain.Modules.InitialLoad.Interfaces;

/// <summary>IL-8A — versiones del ASI de apertura de la empresa (una vigente + historial).</summary>
public interface IOpeningJournalEntryPostingRepository
{
    /// <summary>Versión vigente (tracked) o null si la empresa aún no tiene ASI o la última fue reemplazada.</summary>
    Task<OpeningJournalEntryPosting?> FindCurrentAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );

    /// <summary>IL-8B — versión por Id (tracked; vigente o historial) de la empresa, o null.</summary>
    Task<OpeningJournalEntryPosting?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default
    );

    /// <summary>Última versión usada por la empresa (vigente o historial); 0 si nunca tuvo ASI.</summary>
    Task<int> GetLastVersionAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Serializa todo lo que lee o cambia la apertura de la empresa: <c>FOR UPDATE</c> de la fila de
    /// la empresa (publicación del ASI y cambio de <c>OpeningBalanceDate</c>). Con
    /// <paramref name="includeBalanceBatches"/> también bloquea sus lotes de saldos (mismo bloqueo por
    /// lote que la contabilización IL-7B), para que la cuenta puente no cambie mientras se publica el
    /// ASI. Requiere una transacción abierta en un proveedor relacional.
    /// </summary>
    Task LockCompanyOpeningAsync(
        Guid tenantId,
        Guid companyId,
        bool includeBalanceBatches,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(OpeningJournalEntryPosting posting, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
