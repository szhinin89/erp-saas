namespace ERP.Application.Modules.Purchases.DTOs;

/// <summary>
/// FLOW-READY-02D.1 — proyección de lectura de <c>PurchaseInvoiceTaxSummary</c>, resumen fiscal
/// persistido de una factura de compra confirmada, agrupado por combinación de impuesto.
/// <c>CreditedTaxableBase</c>/<c>AvailableTaxableBase</c> (FLOW-READY-02C-R1.2) reflejan cuánta base
/// ya fue acreditada por notas de crédito de compra tipo Descuento no canceladas, para que la UI de
/// creación de NC pueda mostrar "ya acreditado"/"disponible" sin recalcular desde cero.
/// </summary>
public sealed record PurchaseInvoiceTaxSummaryDto(
    Guid Id,
    string VatCode,
    decimal VatRate,
    string? VatName,
    string? IceCode,
    decimal IceRate,
    string? IceName,
    // FLOW-READY-02F.1 — dimensión IRBPNR, aditiva; siempre null/0 en facturas sin ese impuesto.
    string? IrbpnrCode,
    decimal IrbpnrRate,
    string? IrbpnrName,
    decimal TaxableBase,
    decimal IceAmount,
    decimal VatAmount,
    decimal IrbpnrAmount,
    decimal TotalAmount,
    decimal CreditedTaxableBase,
    decimal AvailableTaxableBase
);
