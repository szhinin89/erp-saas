using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.CrearTransferencia;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public record CreateTransferCommand(
    Guid                        SourceWarehouseId,
    Guid                        TargetWarehouseId,
    string? Reason,
    string? Notes,
    List<TransferItemDto>  Items
) : IRequest<Result<TransferDto>>;

public record TransferItemDto(Guid ProductId, decimal Quantity);

