using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;

namespace ERP.Application.Inventario.UseCases.CancelarTransferencia;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CancelarTransferenciaCommand(Guid TransferenciaId)
    : IRequest<Result<TransferenciaDto>>;
