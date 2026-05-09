using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Bodegas.DTOs;

namespace ERP.Application.Modules.Bodegas.UseCases.UpdateBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record UpdateBodegaCommand(
    Guid    Id,
    Guid    SucursalId,
    string  Nombre,
    string? Ubicacion,
    string? Encargado
) : IRequest<Result<BodegaDto>>;
