namespace ERP.Domain.Modules.SriCatalogs.Enums;

/// <summary>
/// FLOW-READY-02F.1 — cómo se calcula la tarifa de un impuesto SRI no-IVA (ICE/IRBPNR).
/// <see cref="Percentage"/>: tarifa aplicada sobre una base imponible (<c>Percentage</c> del catálogo).
/// <see cref="Specific"/>: monto fijo por unidad/base física (<c>UnitValue</c> del catálogo) — p. ej.
/// IRBPNR (USD por botella) o ICE de bebidas azucaradas (USD por cada 100g de azúcar). Compras nunca
/// recalcula un monto <see cref="Specific"/> desde el catálogo: siempre usa el <c>valor</c> exacto ya
/// declarado en el XML del proveedor (ver <c>PurchaseInvoiceDetailTax.Source</c>).
/// </summary>
public enum SriTaxCalculationType
{
    Percentage = 1,
    Specific = 2,
}
