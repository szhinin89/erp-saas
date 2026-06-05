using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.CancelPurchaseOrder;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record CancelPurchaseOrderCommand(Guid OrderId)
    : IRequest<Result<PurchaseOrderDto>>, ICompanyScopedRequest;
