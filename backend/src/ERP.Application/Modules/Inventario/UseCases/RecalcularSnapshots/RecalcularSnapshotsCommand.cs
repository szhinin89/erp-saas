using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Inventario.UseCases.RecalcularSnapshots;

/// <summary>
/// Recalcula los snapshots diarios del kardex para el tenant actual.
/// Se puede filtrar por producto y/o bodega.
/// Si no se proporciona <c>Hasta</c>, calcula hasta ayer.
/// Retorna el número de snapshots generados o actualizados.
/// </summary>
public sealed record RecalcularSnapshotsCommand(
    Guid?     ProductoId = null,
    Guid?     BodegaId   = null,
    DateTime? Hasta      = null
) : IRequest<Result<int>>;
