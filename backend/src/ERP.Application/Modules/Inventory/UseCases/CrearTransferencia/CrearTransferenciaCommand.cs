using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.CrearTransferencia;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CrearTransferenciaCommand(
    Guid                        SourceWarehouseId,
    Guid                        TargetWarehouseId,
    string? Reason,
    string? Notes,
    List<ItemTransferenciaDto>  Items
) : IRequest<Result<TransferenciaDto>>;

public record ItemTransferenciaDto(Guid ProductId, decimal Quantity);
