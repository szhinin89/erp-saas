using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.CrearVenta;

/// <summary>Creación de venta/factura electrónica; requiere feature de ventas en el plan SaaS.</summary>
[RequireFeature(SubscriptionFeatureCodes.Sales)]
public record CreateSaleCommand(
    Guid             CustomerId,
    Guid             WarehouseId,
    Guid             BranchId,
    List<SaleItemDto> Items,
    /// <summary>Código SRI: "01"=efectivo, "02"=cheque, "03"=crédito, "04"=transferencia, etc. Default "01".</summary>
    string           PaymentMethodCode = "01",
    /// <summary>Plazo en días (0 = contado).</summary>
    short            PaymentDays       = 0,
    /// <summary>Observaciones libres que se emiten en &lt;infoAdicional&gt; del XML SRI.</summary>
    string?          Notes             = null
) : IRequest<Result<Guid>>, ICompanyScopedRequest;

public record SaleItemDto(
    Guid    ProductId,
    decimal Quantity,
    decimal UnitPrice,
    /// <summary>Descuento en valor absoluto sobre el total de la línea (0 = sin descuento).</summary>
    decimal DiscountAmount = 0
);

