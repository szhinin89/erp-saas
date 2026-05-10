using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.GetOrdenCompraById;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record GetOrdenCompraByIdQuery(Guid OrdenId)
    : IRequest<Result<OrdenCompraDetailDto?>>;
