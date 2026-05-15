using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.CrearVenta;

/// <summary>Creación de venta/factura electrónica; requiere feature de ventas en el plan SaaS.</summary>
[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record CrearVentaCommand(
    Guid ClienteId,
    Guid BodegaId,
    Guid SucursalId,
    List<ItemVentaDto> Items
) : IRequest<Result<Guid>>;

public record ItemVentaDto(
    Guid ProductoId,
    decimal Cantidad,
    decimal PrecioUnitario
);