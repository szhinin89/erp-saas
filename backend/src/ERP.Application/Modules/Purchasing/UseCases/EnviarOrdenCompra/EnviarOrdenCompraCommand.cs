using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.EnviarOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record EnviarOrdenCompraCommand(Guid OrdenId)
    : IRequest<Result<OrdenCompraDto>>;
