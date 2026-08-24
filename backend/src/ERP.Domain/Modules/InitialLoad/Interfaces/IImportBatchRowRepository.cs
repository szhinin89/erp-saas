using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Domain.Modules.InitialLoad.Interfaces;

public interface IImportBatchRowRepository
{
    Task AddRangeAsync(
        IEnumerable<ImportBatchRow> rows,
        CancellationToken cancellationToken = default
    );

    Task<(IReadOnlyList<ImportBatchRow> Rows, int TotalCount)> GetPageAsync(
        Guid importBatchId,
        Guid tenantId,
        Guid companyId,
        int pageNumber,
        int pageSize,
        bool? onlyWithBlockingIssue,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Página de filas válidas (sin error bloqueante, aún no importadas) del lote — usado por
    /// ConfirmImportBatch, que pagina en vez de cargar el conjunto completo en memoria. Paginado
    /// (no <c>IAsyncEnumerable</c>) a propósito: confirmar cada fila envía comandos MediatR que
    /// abren su propio <c>SaveChangesAsync</c> sobre el mismo <c>DbContext</c> — mantener un
    /// <c>DataReader</c> abierto (como haría un streaming real) entraría en conflicto con esas
    /// escrituras anidadas en Npgsql.
    /// </summary>
    Task<IReadOnlyList<ImportBatchRow>> GetValidRowsPageAsync(
        Guid importBatchId,
        Guid tenantId,
        Guid companyId,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
