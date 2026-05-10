using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.CancelarOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record CancelarOrdenCompraCommand(Guid OrdenId)
    : IRequest<Result<OrdenCompraDto>>;
