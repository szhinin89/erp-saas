using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.GetOrdenesCompraList;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public record GetPurchaseOrdersListQuery(
    int       PageNumber,
    int       PageSize,
    Guid? BusinessPartnerId,
    string?   Status,
    DateTime? DateFrom,
    DateTime? DateTo
) : IRequest<Result<PurchaseOrdersPagedResult>>, ICompanyScopedRequest;
