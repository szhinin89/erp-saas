using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.SendPurchaseOrder;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record SendOrderPurchaseCommand(Guid OrdenId)
    : IRequest<Result<PurchaseOrderDto>>, ICompanyScopedRequest;
