using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventario.DTOs;

namespace ERP.Application.Modules.Inventario.UseCases.ActualizarBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record UpdateBodegaCommand(
    Guid    Id,
    Guid    SucursalId,
    string  Nombre,
    string? Ubicacion,
    string? Encargado
) : IRequest<Result<BodegaDto>>;
