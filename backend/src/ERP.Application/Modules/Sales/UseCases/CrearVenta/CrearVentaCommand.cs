using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.CrearVenta;

/// <summary>Creación de venta/factura electrónica; requiere feature de ventas en el plan SaaS.</summary>
[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record CrearVentaCommand(
    Guid               CustomerId,
    Guid               WarehouseId,
    Guid               BranchId,
    List<ItemVentaDto> Items
) : IRequest<Result<Guid>>;

public record ItemVentaDto(
    Guid    ProductId,
    decimal Quantity,
    decimal UnitPrice
);