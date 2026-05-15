using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.GetOrdenesCompraList;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record GetOrdenesCompraListQuery(
    int       PageNumber,
    int       PageSize,
    Guid?     ProveedorId,
    string?   Estado,
    DateTime? FechaDesde,
    DateTime? FechaHasta
) : IRequest<Result<OrdenesCompraPagedResult>>;
