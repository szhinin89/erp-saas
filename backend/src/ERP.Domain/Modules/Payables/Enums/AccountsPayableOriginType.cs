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
}
