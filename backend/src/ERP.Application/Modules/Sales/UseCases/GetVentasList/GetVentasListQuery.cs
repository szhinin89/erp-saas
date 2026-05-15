using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;

namespace ERP.Application.Sales.UseCases.GetVentasList;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public record GetSalesListQuery(
    int       PageNumber  = 1,
    int       PageSize    = 20,
    Guid?     CustomerId  = null,
    DateTime? DateFrom  = null,
    DateTime? DateTo  = null,
    string?   Status      = null,
    string?   Search      = null
) : IRequest<Result<SalesPagedResult>>;
