namespace ERP.Application.Modules.Sales.DTOs;

/// <summary>
/// Valores por defecto para inicializar una nueva factura de venta.
/// Los campos nullable son opcionales: si la empresa no los configuró, el frontend
/// debe usar el catálogo disponible sin asumir un valor concreto.
/// Los campos Fallback* son el único punto centralizado de fallback del sistema
/// para campos obligatorios cuyo catálogo siempre debe tener un valor seleccionado.
/// </summary>
public sealed record SalesInvoiceDefaultsDto(
    string?  DefaultDocTypeCode,
    string?  DefaultSriPaymentMethodCode,
    Guid?    DefaultEmissionPointId,
    Guid?    DefaultWarehouseId,
    Guid?    DefaultPaymentTermId,
    string   FallbackDocTypeCode,
    string   FallbackSriPaymentMethodCode);

public sealed record SalesInvoiceDto(
    Guid Id, Guid CustomerId, string CustomerName, string CustomerTaxId,
    string CustomerIdentificationType, string? CustomerEmail, string? CustomerAddress,
    string DocTypeCode, string? SriPaymentMethodCode, string InvoiceNumber, DateOnly IssueDate,
    Guid CashSessionId, Guid? EmissionPointId, string EmissionType,
    string CurrencyCode, decimal ExchangeRate,
    Guid PaymentTermId, string PaymentTermName,
    int PaymentTermInstallments, int PaymentTermDaysBetween, int CreditTermDays,
    DateOnly? DueDate, string? Notes, string Status, string ElectronicStatus,
    string? AccessKey, string? AuthorizationNumber, DateTime? AuthorizationDate,
    decimal Subtotal, decimal TotalDiscount, decimal TotalIce, decimal TotalVat, decimal TotalTax,
    decimal GrandTotal,
    IReadOnlyList<SalesInvoicePaymentDto> Payments,
    IReadOnlyList<SalesInvoiceDetailDto> Lines,
    DateTime CreatedAt, DateTime? UpdatedAt,
    /// <summary>
    /// Motivo por el que falló la emisión del documento electrónico en el intento más reciente
    /// (solo poblado por <c>AuthorizeSalesInvoiceHandler</c> cuando el intento ocurrió en esta
    /// misma respuesta). Null si no se intentó, si la factura no es electrónica, o si tuvo éxito.
    /// </summary>
    string? ElectronicIssueError = null);

public sealed record SalesInvoicePaymentDto(
    Guid Id, Guid PaymentMethodId, string PaymentMethodCode,
    string PaymentMethodName, decimal Amount, string? Reference,
    PaymentCardDetailDto? CardDetail,
    PaymentTransferDetailDto? TransferDetail,
    PaymentChequeDetailDto? ChequeDetail);

public sealed record PaymentCardDetailDto(
    string? CardBrand, string? CardLastFour, string? BankName,
    string? AuthorizationCode, string? LotNumber);

public sealed record PaymentTransferDetailDto(
    string? BankName, string? ReceiptNumber, DateOnly? TransferDate);

public sealed record PaymentChequeDetailDto(
    string? BankName, string? ChequeNumber, string? HolderName, DateOnly? CashDate);

public sealed record SalesInvoiceDetailDto(
    Guid Id, Guid? ItemId, Guid? WarehouseId, string Description,
    string? SnapshotSku, string? SnapshotItemName,
    string UomCode, decimal ConversionFactor, decimal QuantityInBaseUom,
    decimal Quantity, decimal UnitPrice,
    decimal DiscountPct, decimal DiscountAmount,
    decimal TaxableBase,
    string VatCode, decimal VatRate, decimal VatAmount, string? SnapshotVatName,
    string? IceCode, decimal IceRate, decimal IceAmount, string? SnapshotIceName,
    decimal TaxInclusiveTotal,
    string? Notes, short SortOrder);

/// <summary>
/// Resultado de resolver el precio e impuestos oficiales de un ítem para una línea
/// de venta, vía IPricingResolver (Pricing Engine v2) + ISriTaxResolver. UnitPrice
/// es el precio neto SSOT — el frontend lo usa para inicializar la línea al
/// seleccionar el producto; no debe calcularse localmente.
/// </summary>
public sealed record SalesItemPricingDto(
    Guid ItemId, decimal UnitPrice,
    string? VatCode, string? VatName,
    string? IceCode, string? IceName,
    decimal? MaxDiscountPercent,
    string PriceListCode);

public sealed record SalesListDto(
    Guid Id, string InvoiceNumber, DateOnly IssueDate, Guid CustomerId,
    string CustomerName, string Status, int LineCount,
    decimal GrandTotal, DateTime CreatedAt);

/// <summary>
/// Raw result returned by IInvoiceItemSearchRepository.
/// Contains tax codes but NOT display strings or FinalSalePrice
/// — those are computed in SearchItemsForInvoiceHandler via ISriCatalogResolver
/// + SriTaxCalculator to guarantee a single source of truth.
/// </summary>
public sealed record InvoiceItemMatch(
    Guid     Id,
    string   Sku,
    string   Description,
    string?  ProductFamilyName,
    string   UomAbbrev,
    bool     TracksStock,
    string?  WarehouseName,
    decimal? AvailableStock,
    decimal? AverageCost,
    decimal? SalePriceWithoutTax,
    string?  VatCode,
    string?  IceCode);

public sealed record InvoiceItemSearchResultDto(
    Guid     Id,
    string   Sku,
    string   Description,
    string?  ProductFamilyName,
    string   UomAbbrev,
    bool     TracksStock,
    string?  WarehouseName,
    decimal? AvailableStock,
    decimal? AverageCost,
    decimal? SalePriceWithoutTax,
    decimal? FinalSalePrice,
    string   VatDisplay,
    string   IceDisplay,
    string?  VatCode,
    string?  IceCode);
