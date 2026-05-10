using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;

namespace ERP.Application.Inventario.UseCases.GetAjustesList;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record GetAjustesListQuery(
    int       PageNumber,
    int       PageSize,
    Guid?     BodegaId,
    Guid?     ProductoId,
    string?   Estado,
    DateTime? FechaDesde,
    DateTime? FechaHasta
) : IRequest<Result<AjustesPagedResult>>;
