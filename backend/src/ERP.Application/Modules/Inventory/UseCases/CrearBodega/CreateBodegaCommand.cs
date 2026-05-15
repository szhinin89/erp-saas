using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.CrearBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CreateBodegaCommand(
    Guid    SucursalId,
    string  Nombre,
    string? Ubicacion,
    string? Encargado
) : IRequest<Result<BodegaDto>>;
