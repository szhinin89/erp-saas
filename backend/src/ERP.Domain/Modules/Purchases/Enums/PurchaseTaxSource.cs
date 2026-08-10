namespace ERP.Domain.Modules.Purchases.Enums;

/// <summary>
/// FLOW-READY-02F.1 — origen del monto persistido en <c>PurchaseInvoiceDetailTax</c>.
/// <see cref="Xml"/>: el monto es el <c>valor</c> exacto declarado por el proveedor en el XML
/// autorizado (nunca se recalcula). <see cref="Calculated"/>: la línea es manual (sin XML de origen)
/// y el monto se calculó a partir de la tarifa porcentual del catálogo SRI — solo aplica a IVA/ICE
/// porcentual; no hay flujo manual para IRBPNR en esta fase.
/// </summary>
public enum PurchaseTaxSource
{
    Xml = 1,
    Calculated = 2,
}
