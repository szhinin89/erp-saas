using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.CrearOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record CrearOrdenCompraCommand(
    Guid                          ProveedorId,
    DateTime                      FechaRequerida,
    Guid?                         BodegaDestinoId,
    string?                       DireccionEntrega,
    string?                       Observaciones,
    List<ItemOrdenCompraRequest>  Items
) : IRequest<Result<OrdenCompraDto>>;
