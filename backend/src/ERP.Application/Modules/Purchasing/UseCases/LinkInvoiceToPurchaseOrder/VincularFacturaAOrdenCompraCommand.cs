using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.LinkInvoiceToPurchaseOrder;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record LinkInvoiceToPurchaseOrderCommand(
    Guid OrdenCompraId,
    Guid PurchBillId
) : IRequest<Result<PurchaseOrderDto>>, ICompanyScopedRequest;
