using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.GetTransferenciasList;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record GetTransferenciasListQuery(
    int       PageNumber      = 1,
    int       PageSize        = 20,
    Guid?     BodegaOrigenId  = null,
    Guid?     BodegaDestinoId = null,
    string?   Estado          = null,
    DateTime? FechaDesde      = null,
    DateTime? FechaHasta      = null
) : IRequest<Result<TransferenciasPagedResult>>;
