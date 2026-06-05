using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;

namespace ERP.Application.Modules.Cash.Services;

public interface IReconciliationService
{
    Task<Result<IReadOnlyList<ReconciliationSuggestionDto>>> SugerirConciliacionAsync(Guid StatementId, CancellationToken ct);

    Task<Result<bool>> ConciliarMovimientoAsync(Guid movimientoId, Guid asientoContableId, CancellationToken ct);
}
