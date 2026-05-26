using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.ApprovePurchaseOrder;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record ApproveOrderPurchaseCommand(Guid OrdenId)
    : IRequest<Result<PurchaseOrderDto>>, ICompanyScopedRequest;
