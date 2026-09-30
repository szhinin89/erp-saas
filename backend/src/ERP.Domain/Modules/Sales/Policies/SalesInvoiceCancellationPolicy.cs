using ERP.Domain.Exceptions;

namespace ERP.Domain.Modules.Sales.Policies;

/// <summary>
/// ZH-SALES-CANCEL-AUTHORIZED-RETURN-RULE-01 — única fuente de verdad de "¿esta factura de venta
/// puede anularse considerando sus devoluciones?". Una devolución <c>Authorized</c> ya reingresó
/// Kardex, reembolsó (efectivo o crédito a CxC), contabilizó y emitió su Nota de Crédito, y en
/// Ventas es terminal (<c>SalesReturn.Cancel()</c> solo desde Draft); la anulación revierte la
/// factura completa. Anular encima duplicaría inventario, reembolso y contabilidad, así que una
/// factura con una devolución autorizada no se anula — mismo criterio que Compras (PI-CANC-01).
/// Una devolución <c>Draft</c> no bloquea: no tiene efectos, y sobre la factura anulada ya no puede
/// autorizarse. Invocada por <c>CancelSalesInvoiceHandler</c> bajo el lock de la factura.
/// </summary>
public static class SalesInvoiceCancellationPolicy
{
    public const string AuthorizedReturnMessage =
        "La factura tiene una devolución autorizada y no puede anularse.";

    public static void EnsureCanCancel(bool hasAuthorizedReturns)
    {
        if (hasAuthorizedReturns)
            throw new DomainRuleViolationException(AuthorizedReturnMessage);
    }
}
