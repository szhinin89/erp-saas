namespace ERP.Domain.Modules.Finance.Enums;

/// <summary>
/// Dirección del movimiento de dinero que representa un <c>Payment</c> (Fase 5.5.5.2): un único
/// agregado sirve tanto para cobros (AR, contra <c>SalesReceivable</c>) como para pagos (AP,
/// contra <c>PurchasePayable</c>) — la estructura es idéntica (cabecera + líneas de aplicación +
/// invariante de balance), solo cambia qué tipo de documento referencian las líneas. Mismo
/// criterio que discriminar por <c>SourceModule</c> en el Posting Engine (ADR-026) en lugar de
/// duplicar el agregado.
/// </summary>
public enum PaymentDirection
{
    /// <summary>Cobro a cliente — las líneas de aplicación referencian <c>SalesReceivable</c>.</summary>
    Collection,

    /// <summary>Pago a proveedor — las líneas de aplicación referencian <c>PurchasePayable</c>.</summary>
    Payment,
}
