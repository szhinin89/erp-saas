using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;

namespace ERP.Application.Modules.Caja.Services;

public interface IConciliacionService
{
    Task<Result<IReadOnlyList<SugerenciaConciliacionDto>>> SugerirConciliacionAsync(Guid extractoId, CancellationToken ct);

    Task<Result<bool>> ConciliarMovimientoAsync(Guid movimientoId, Guid asientoContableId, CancellationToken ct);
}
