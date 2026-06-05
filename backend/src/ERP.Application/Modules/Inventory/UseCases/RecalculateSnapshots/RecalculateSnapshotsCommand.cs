using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Inventory.UseCases.RecalculateSnapshots;

/// <summary>
/// Recalcula los snapshots diarios del kardex para el tenant actual.
/// Se puede filtrar por producto y/o Warehouse.
/// Si no se proporciona <c>Hasta</c>, calcula hasta ayer.
/// Retorna el n�mero de snapshots generados o actualizados.
///
/// note operativa: este comando debe ejecutarse antes de activar
/// <c>UseScalableMode=true</c> en producci�n para preparar los saldos peri�dicos.
/// </summary>
public sealed record RecalculateSnapshotsCommand(
    Guid?     ProductId = null,
    Guid?     WarehouseId   = null,
    DateTime? DateTo     = null
) : IRequest<Result<int>>, ICompanyScopedRequest;
