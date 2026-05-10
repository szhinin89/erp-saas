using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;

namespace ERP.Application.Inventario.UseCases.GetTransferenciaById;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record GetTransferenciaByIdQuery(Guid Id)
    : IRequest<Result<TransferenciaDetailDto?>>;
