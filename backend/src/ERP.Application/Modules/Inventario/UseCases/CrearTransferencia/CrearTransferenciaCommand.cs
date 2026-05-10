using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;

namespace ERP.Application.Inventario.UseCases.CrearTransferencia;

[RequireFeature(SubscriptionFeatureCodes.Inventory)]
public sealed record CrearTransferenciaCommand(
    Guid                        BodegaOrigenId,
    Guid                        BodegaDestinoId,
    string?                     Motivo,
    string?                     Observaciones,
    List<ItemTransferenciaDto>  Items
) : IRequest<Result<TransferenciaDto>>;

public record ItemTransferenciaDto(Guid ProductoId, decimal Cantidad);
