using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.CrearVenta;

/// <summary>Creación de venta/factura electrónica; requiere feature de ventas en el plan SaaS.</summary>
[RequireFeature(SubscriptionFeatureCodes.Sales)]
public record CreateSaleCommand(
    Guid               CustomerId,
    Guid               WarehouseId,
    Guid               BranchId,
    List<SaleItemDto> Items
) : IRequest<Result<Guid>>;

public record SaleItemDto(
    Guid    ProductId,
    decimal Quantity,
    decimal UnitPrice
);

