namespace ERP.Application.Modules.Sales.DTOs;

/// <summary>
/// Valores por defecto para inicializar una nueva factura de venta.
/// Los campos nullable son opcionales: si la empresa no los configuró, el frontend
/// debe usar el catálogo disponible sin asumir un valor concreto.
/// Los campos Fallback* son el único punto centralizado de fallback del sistema
/// para campos obligatorios cuyo catálogo siempre debe tener un valor seleccionado.
///
/// CONFIG-FOUNDATION-P0-01: <c>DefaultWarehouseId</c> ahora se resuelve en backend (Branch
/// OrgSetting → Warehouse.IsMain de la sucursal → null). El frontend NUNCA debe elegir la
/// primera bodega de un listado como sustituto — si <c>DefaultWarehouseId</c> es null y
/// <c>RequiresManualWarehouseSelection</c> es true, debe exigir selección manual.
/// <c>DefaultWarehouseSource</c> es uno de: "BranchSetting" | "BranchMainWarehouse" | "None"
/// (el valor "CashRegister" —de mayor precedencia— se resuelve en frontend a partir de la
/// sesión de caja activa, fuera de esta query; ver Fase 3/4 de
/// docs/architecture/configuration-engine-target-architecture.md).
/// <c>ConfigurationWarnings</c> solo se puebla cuando un valor configurado es inválido
/// (fail-closed: nunca cae en silencio al siguiente scope).
/// </summary>
public sealed record SalesInvoiceDefaultsDto(
    string? DefaultDocTypeCode,
    string? DefaultSriPaymentMethodCode,
    Guid? DefaultEmissionPointId,
    Guid? DefaultWarehouseId,
    Guid? DefaultPaymentTermId,
    string FallbackDocTypeCode,
    string FallbackSriPaymentMethodCode,
    string DefaultWarehouseSource,
    bool RequiresManualWarehouseSelection,
    IReadOnlyList<string> ConfigurationWarnings
);

public sealed record SalesInvoiceDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string CustomerTaxId,
    string CustomerIdentificationType,
    string? CustomerEmail,
    string? CustomerAddress,
    // SALES-PRICING-TRACEABILITY-SNAPSHOT-07B: snapshot histórico de la lista de precios EXPLÍCITA
    // del cliente (PriceListCustomer) al momento de vender — null si el cliente no tenía ninguna
    // asignada. NUNCA es la lista default de la empresa; eso se resuelve por línea (ver
    // SalesInvoiceDetailDto.SelectionSource). Informativo para la cabecera de la UI — cada línea
    // puede haber usado una lista distinta si el ítem no estaba asignado a esta.
    Guid? CustomerPreferredPriceListId,
    string? CustomerPreferredPriceListName,
    // SALES-PRICING-TRACEABILITY-VERSION-07B1: null = documento anterior a 07B, trazabilidad de
    // selección NO capturada — CustomerPreferredPriceListId/Name y
    // SalesInvoiceDetailDto.SelectionSource en null NUNCA deben leerse como "sin lista"/"PVP" en
    // ese caso. 1 = captura vigente, donde esos mismos null sí son un dato real.
    int? PricingTraceabilityVersion,
    string DocTypeCode,
    string? SriPaymentMethodCode,
    string InvoiceNumber,
    DateOnly IssueDate,
    Guid CashSessionId,
    Guid? EmissionPointId,
    string EmissionType,
    string CurrencyCode,
    decimal ExchangeRate,
    Guid PaymentTermId,
    string PaymentTermName,
    int PaymentTermInstallments,
    int PaymentTermDaysBetween,
    int CreditTermDays,
    DateOnly? DueDate,
    string? Notes,
    string Status,
    string ElectronicStatus,
    string? AccessKey,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalIce,
    decimal TotalVat,
    decimal TotalTax,
    decimal GrandTotal,
    IReadOnlyList<SalesInvoicePaymentDto> Payments,
    IReadOnlyList<SalesInvoiceDetailDto> Lines,
    IReadOnlyList<SalesPaymentScheduleDto> PaymentSchedule,
    bool IsPaymentScheduleManual,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    /// <summary>
    /// Motivo por el que falló la emisión del documento electrónico en el intento más reciente
    /// (solo poblado por <c>AuthorizeSalesInvoiceHandler</c> cuando el intento ocurrió en esta
    /// misma respuesta). Null si no se intentó, si la factura no es electrónica, o si tuvo éxito.
    /// </summary>
    string? ElectronicIssueError = null
);

public sealed record SalesPaymentScheduleDto(
    Guid Id,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    string? Notes
);

public sealed record SalesInvoicePaymentDto(
    Guid Id,
    Guid PaymentMethodId,
    string PaymentMethodCode,
    string PaymentMethodName,
    decimal Amount,
    string? Reference,
    PaymentCardDetailDto? CardDetail,
    PaymentTransferDetailDto? TransferDetail,
    PaymentChequeDetailDto? ChequeDetail
);

public sealed record PaymentCardDetailDto(
    string? CardBrand,
    string? CardLastFour,
    string? BankName,
    string? AuthorizationCode,
    string? LotNumber
);

public sealed record PaymentTransferDetailDto(
    Guid? CompanyBankAccountId,
    string? BankName,
    string? ReceiptNumber,
    DateOnly? TransferDate
);

public sealed record PaymentChequeDetailDto(
    string? BankName,
    string? ChequeNumber,
    string? HolderName,
    DateOnly? CashDate
);

public sealed record SalesInvoiceDetailDto(
    Guid Id,
    Guid? ItemId,
    Guid? WarehouseId,
    string Description,
    string? SnapshotSku,
    string? SnapshotItemName,
    string UomCode,
    decimal ConversionFactor,
    decimal QuantityInBaseUom,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal DiscountAmount,
    decimal TaxableBase,
    string VatCode,
    decimal VatRate,
    decimal VatAmount,
    string? SnapshotVatName,
    string? IceCode,
    decimal IceRate,
    decimal IceAmount,
    string? SnapshotIceName,
    decimal TaxInclusiveTotal,
    string? Notes,
    short SortOrder,
    Guid? PackagingLevelId,
    string BaseUomCode,
    // ── SALES-HISTORICAL-PRICING-SNAPSHOT-01 — snapshot comercial histórico, congelado en el
    // Draft y nunca recalculado en Authorize. Todos nullable: null = dato no disponible al
    // momento de vender (nunca se fabrica un valor al leer). ──────────────────────────────
    string? WarehouseName,
    decimal? UnitCostAtSale,
    decimal? TotalCostAtSale,
    decimal? ListPriceAtSale,
    Guid? PriceListId,
    string? PriceListName,
    string? PricingSource,
    string? DiscountSource,
    string? DiscountDescription,
    // SALES-PRICING-TRACEABILITY-SNAPSHOT-07B: "Customer" | "CompanyDefault" | null (PVP) — de
    // qué candidato salió PriceListId/PriceListName, snapshot puro (nunca un enum de Pricing acá,
    // ver SalesInvoiceDetail.SelectionSource).
    string? SelectionSource
);

public sealed record SalesReceiptPrintPayloadDto(
    Guid TenantId,
    Guid CompanyId,
    Guid BranchId,
    string CompanyName,
    string? TradeName,
    string Ruc,
    string BranchName,
    string? EstablishmentCode,
    string? EmissionPointCode,
    string? CashRegisterName,
    Guid? CashSessionId,
    Guid InvoiceId,
    string InvoiceNumber,
    DateOnly IssuedAt,
    string CustomerName,
    string CustomerIdentification,
    string? CustomerEmail,
    string DocumentType,
    bool IsElectronic,
    string? ElectronicStatus,
    string? AccessKey,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    IReadOnlyList<SalesReceiptLineDto> Lines,
    SalesReceiptTotalsDto Totals,
    IReadOnlyList<SalesReceiptPaymentDto> Payments,
    decimal? CashReceived,
    decimal? CashChange,
    string? FooterMessage
);

public sealed record SalesReceiptLineDto(
    string ProductName,
    string? Sku,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal,
    decimal VatRate,
    decimal VatAmount,
    decimal Total,
    // SALES-PRESENTATIONS-04: UomCode/ConversionFactor son la presentación VISIBLE vendida (ej.
    // "CAJA" x12) — nunca QuantityInBaseUom/BaseUomCode, que solo son de stock/kardex. Con
    // ConversionFactor=1 (sin presentación, comportamiento actual) el frontend no debe mostrar
    // ninguna etiqueta adicional.
    string UomCode,
    decimal ConversionFactor
);

public sealed record SalesReceiptPaymentDto(string Method, decimal Amount, string? Reference);

public sealed record SalesReceiptTotalsDto(
    decimal SubtotalWithoutTaxes,
    decimal DiscountTotal,
    decimal VatTotal,
    decimal Total
);

/// <summary>
/// Resultado de resolver el precio e impuestos oficiales de un ítem para una línea
/// de venta, vía IPricingResolver (Pricing Engine v2) + ISriTaxResolver. UnitPrice
/// es el precio neto SSOT — el frontend lo usa para inicializar la línea al
/// seleccionar el producto; no debe calcularse localmente.
/// SALES-PRICE-LIST-DISCOUNT-VISIBILITY-01: BasePrice/PriceListName/DiscountDescription
/// se agregan solo para que la UI explique de dónde sale UnitPrice cuando difiere del precio
/// base del ítem — no participan en ningún cálculo, son el mismo BasePrice/PriceListName/
/// RuleDescription ya resueltos por PricingResolver (PricingResult), nunca recalculados aquí.
/// DiscountDescription es null cuando UnitPrice == BasePrice (sin regla aplicada).
/// </summary>
public sealed record SalesItemPricingDto(
    Guid ItemId,
    decimal UnitPrice,
    string? VatCode,
    string? VatName,
    string? IceCode,
    string? IceName,
    decimal? MaxDiscountPercent,
    string PriceListCode,
    decimal BasePrice,
    string PriceListName,
    string? DiscountDescription,
    // SALES-PRICING-UX-TRACEABILITY-07C: null = ninguna lista aplicó (PVP). Permite a la UI
    // distinguir PVP real del sentinel de nombre "Precio base" sin hardcodear ese texto.
    Guid? PriceListId
);

public sealed record SalesListDto(
    Guid Id,
    string InvoiceNumber,
    DateOnly IssueDate,
    Guid CustomerId,
    string CustomerName,
    string Status,
    int LineCount,
    decimal GrandTotal,
    DateTime CreatedAt
);

/// <summary>
/// Raw result returned by IInvoiceItemSearchRepository.
/// Contains tax codes but NOT display strings or FinalSalePrice
/// — those are computed in SearchItemsForInvoiceHandler via ISriCatalogResolver
/// + SriTaxCalculator to guarantee a single source of truth.
/// </summary>
/// <summary>
/// SALES-PRESENTATIONS-03: presentación vendible de un ítem (ItemPackagingLevel activo),
/// expuesta al buscador de ventas para que el cajero pueda elegir unidad/caja/pack. Espejo del
/// mismo concepto que Compras ya expone en <c>PurchaseItemPackagingLevelDto</c> — no se reutiliza
/// el mismo DTO porque viven en módulos/contratos de API distintos, pero la forma es idéntica a
/// propósito.
/// </summary>
public sealed record InvoiceItemPackagingLevelDto(
    Guid Id,
    string Name,
    string UomCode,
    decimal BaseQuantity,
    string? Barcode,
    bool IsBaseUnit,
    bool IsSaleDefault
);

public sealed record InvoiceItemMatch(
    Guid Id,
    string Sku,
    string Description,
    string? ProductFamilyName,
    string UomAbbrev,
    bool TracksStock,
    string? WarehouseName,
    decimal? AvailableStock,
    decimal? AverageCost,
    decimal? SalePriceWithoutTax,
    string? VatCode,
    string? IceCode,
    string BaseUomCode,
    IReadOnlyList<InvoiceItemPackagingLevelDto> PackagingLevels,
    Guid? MatchedPackagingLevelId
);

/// <summary>
/// SALES-PRICE-LIST-DISCOUNT-VISIBILITY-01 / SALES-CONTEXTUAL-PRICING-READ-06A:
/// SalePriceWithoutTax/FinalSalePrice siguen siendo el precio base sin resolver (ver comentario
/// de InvoiceItemMatch) — DiscountedSalePriceWithoutTax/DiscountedFinalSalePrice son el mismo
/// precio ya resuelto por IPricingResolver.ResolveManyAsync (Customer → CompanyDefault → PVP,
/// nunca recalculado en Sales), solo para que el buscador pueda anticipar "este ítem trae
/// descuento de lista" antes de seleccionarlo. Null cuando ningún candidato (cliente ni default)
/// tiene el ítem asignado, o la lista aplicable no trae ningún ajuste (BasePrice == precio
/// resuelto): en ese caso el precio final real es el ya mostrado arriba.
/// </summary>
public sealed record InvoiceItemSearchResultDto(
    Guid Id,
    string Sku,
    string Description,
    string? ProductFamilyName,
    string UomAbbrev,
    bool TracksStock,
    string? WarehouseName,
    decimal? AvailableStock,
    decimal? AverageCost,
    decimal? SalePriceWithoutTax,
    decimal? FinalSalePrice,
    string VatDisplay,
    string IceDisplay,
    string? VatCode,
    string? IceCode,
    string BaseUomCode,
    IReadOnlyList<InvoiceItemPackagingLevelDto> PackagingLevels,
    Guid? MatchedPackagingLevelId,
    string? PriceListName,
    string? DiscountDescription,
    decimal? DiscountedSalePriceWithoutTax,
    decimal? DiscountedFinalSalePrice
);
