using MediatR;
using ERP.Application.Common;
using ERP.Application.Ventas.DTOs;

namespace ERP.Application.Ventas.UseCases.GetVentaById;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record GetVentaByIdQuery(Guid Id) : IRequest<Result<VentasFacturaDetailDto?>>;
