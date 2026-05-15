using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;

namespace ERP.Application.Sales.UseCases.GetVentaById;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record GetSaleByIdQuery(Guid Id) : IRequest<Result<SalesBillDetailDto?>>;
