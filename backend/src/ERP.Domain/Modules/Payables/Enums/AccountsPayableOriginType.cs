namespace ERP.Domain.Modules.Payables.Enums;

/// <summary>
/// PAYABLES-GENERIC-FOUNDATION-09 — módulo de origen que generó la obligación con el proveedor.
/// Compra/Gasto son documentos de origen; CxP es la deuda viva, desacoplada de ambos. Extensión
/// futura únicamente como valor nuevo al final del enum (persistido como int en BD).
/// </summary>
public enum AccountsPayableOriginType
{
    PurchaseInvoice,
    ExpenseDocument,
    Manual,

    /// <summary>
    /// IL-6A — saldo pendiente de un documento de proveedor al corte (Carga Inicial de CxP). Nunca
    /// reconstruye la compra/gasto ni los pagos/retenciones históricos: <c>OriginId</c> es la fila
    /// del lote de importación que lo cargó, y la CxP lleva su propio número normalizado y lote.
    /// </summary>
    InitialBalance,
}
