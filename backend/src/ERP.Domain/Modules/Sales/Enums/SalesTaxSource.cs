namespace ERP.Domain.Modules.Sales.Enums;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3) — origen del monto persistido en
/// <c>SalesInvoiceDetailTax</c>. A diferencia de Compras (que puede recibir el monto exacto de un
/// XML de proveedor), Ventas siempre calcula el impuesto desde la configuración tributaria vigente
/// del ítem/empresa (ADR-032 §5) — por eso hoy solo existe <see cref="Calculated"/>. El valor queda
/// definido como enum (no solo una constante) para no requerir una migración de esquema si en el
/// futuro Ventas necesita distinguir otro origen.
/// </summary>
public enum SalesTaxSource
{
    Calculated = 1,
}
