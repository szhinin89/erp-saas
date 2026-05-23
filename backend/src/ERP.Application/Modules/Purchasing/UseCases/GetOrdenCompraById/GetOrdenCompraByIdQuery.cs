using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.GetOrdenCompraById;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public record GetPurchaseOrderByIdQuery(Guid OrderId)
    : IRequest<Result<PurchaseOrderDetailDto?>>, ICompanyScopedRequest;
