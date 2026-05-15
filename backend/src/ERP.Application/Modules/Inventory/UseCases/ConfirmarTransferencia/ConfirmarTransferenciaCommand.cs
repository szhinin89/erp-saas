using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.ConfirmarTransferencia;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record ConfirmarTransferenciaCommand(Guid TransferenciaId)
    : IRequest<Result<TransferenciaDto>>;
