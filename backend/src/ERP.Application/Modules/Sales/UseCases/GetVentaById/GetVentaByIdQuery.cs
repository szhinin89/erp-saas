using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;

namespace ERP.Application.Sales.UseCases.GetVentaById;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record GetVentaByIdQuery(Guid Id) : IRequest<Result<VentasFacturaDetailDto?>>;
