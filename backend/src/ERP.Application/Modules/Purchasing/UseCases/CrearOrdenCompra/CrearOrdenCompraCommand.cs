using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;

[RequireFeature(SubscriptionFeatureCodes.Purchases)]
public sealed record CrearOrdenCompraCommand(
    Guid                          ProveedorId,
    DateTime                      FechaRequerida,
    Guid?                         BodegaDestinoId,
    string?                       DireccionEntrega,
    string?                       Observaciones,
    List<ItemOrdenCompraRequest>  Items
) : IRequest<Result<OrdenCompraDto>>;
