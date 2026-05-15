using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.EnviarOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record EnviarOrderPurchaseCommand(Guid OrdenId)
    : IRequest<Result<PurchaseOrderDto>>;
