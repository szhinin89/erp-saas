using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.CancelarOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record CancelarOrderPurchaseCommand(Guid OrdenId)
    : IRequest<Result<PurchaseOrderDto>>;
