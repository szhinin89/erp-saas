using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Inventory.UseCases.RecalcularSnapshots;

/// <summary>
/// Recalcula los snapshots diarios del kardex para el tenant actual.
/// Se puede filtrar por producto y/o Warehouse.
/// Si no se proporciona <c>Hasta</c>, calcula hasta ayer.
/// Retorna el número de snapshots generados o actualizados.
///
/// Nota operativa: este comando debe ejecutarse antes de activar
/// <c>UseScalableMode=true</c> en producción para preparar los saldos periódicos.
/// </summary>
public sealed record RecalcularSnapshotsCommand(
    Guid?     ProductId = null,
    Guid?     WarehouseId   = null,
    DateTime? DateTo     = null
) : IRequest<Result<int>>, ICompanyScopedRequest;
