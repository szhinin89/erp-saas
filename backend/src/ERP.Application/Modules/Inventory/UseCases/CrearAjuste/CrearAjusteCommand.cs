using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.CrearAjuste;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CrearAjusteCommand(
    Guid    BodegaId,
    Guid    ProductoId,
    decimal CantidadAjuste,
    string  Motivo,
    string? Observaciones
) : IRequest<Result<AjusteInventarioDto>>;
