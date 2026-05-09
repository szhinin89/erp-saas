using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Bodegas.DTOs;

namespace ERP.Application.Modules.Bodegas.UseCases.CreateBodega;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CreateBodegaCommand(
    Guid    SucursalId,
    string  Nombre,
    string? Ubicacion,
    string? Encargado
) : IRequest<Result<BodegaDto>>;
