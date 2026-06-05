using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.CreateSale;

/// <summary>Creación de venta/salesBill electrónica; requiere feature de ventas en el plan SaaS.</summary>
[RequireFeature(SubscriptionFeatureCodes.Sales)]
public record CreateSaleCommand(
    Guid             BusinessPartnerId,
    Guid             WarehouseId,
    Guid             BranchId,
    List<SaleItemDto> Items,
    string           PaymentMethodCode  = "01",
    short            PaymentDays        = 0,
    string?          Notes              = null,
    Guid?            SalesOrderPublicId = null,
    Guid?            EmissionPointId    = null
) : IRequest<Result<Guid>>, ICompanyScopedRequest;

public record SaleItemDto(
    Guid    ProductId,
    decimal Quantity,
    decimal UnitPrice,
    /// <summary>Descuento en valor absoluto sobre el total de la línea (0 = sin descuento).</summary>
    decimal DiscountAmount = 0
);

