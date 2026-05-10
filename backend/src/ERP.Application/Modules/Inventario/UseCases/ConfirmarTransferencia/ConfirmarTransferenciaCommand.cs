using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;

namespace ERP.Application.Inventario.UseCases.ConfirmarTransferencia;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record ConfirmarTransferenciaCommand(Guid TransferenciaId)
    : IRequest<Result<TransferenciaDto>>;
