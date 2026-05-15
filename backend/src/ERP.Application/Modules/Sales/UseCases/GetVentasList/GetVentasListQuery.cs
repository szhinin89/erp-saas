using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;

namespace ERP.Application.Sales.UseCases.GetVentasList;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record GetVentasListQuery(
    int       PageNumber  = 1,
    int       PageSize    = 20,
    Guid?     ClienteId   = null,
    DateTime? FechaDesde  = null,
    DateTime? FechaHasta  = null,
    string?   Estado      = null,
    string?   Search      = null
) : IRequest<Result<VentasPagedResult>>;
