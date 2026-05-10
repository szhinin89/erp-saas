using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.EnviarOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record EnviarOrdenCompraCommand(Guid OrdenId)
    : IRequest<Result<OrdenCompraDto>>;
