namespace ERP.Application.Modules.Sales.Exceptions;

/// <summary>
/// SALES-JOURNAL-ENTRY-SILENT-FAILURE-01 Lote 2 — lanzada por
/// <c>SalesInvoiceAuthorizedPostingTranslator</c> cuando <c>IPostingEngine.PostAsync</c> falla para
/// el asiento obligatorio <c>Sales/InvoiceIssued</c>. Antes de este lote, un fallo de posting solo
/// generaba un <c>LogWarning</c> — la factura quedaba <c>Authorized</c> sin ningún asiento contable
/// y sin que nadie se enterara (hallazgo real: dos facturas de venta autorizadas en BD dev sin
/// asiento InvoiceIssued, confirmado en <c>erp-20260913.txt</c>). Mismo criterio ya usado por
/// <c>ExpensePostingFailedException</c> (Gastos) y <c>SupplierPaymentPostingFailedException</c>
/// (Pagos a proveedor): "no autorizar sin asiento" — lanzar aquí, dentro del <c>Handle</c> de un
/// <c>INotificationHandler</c> publicado por <c>ErpDbContext.SaveChangesAsync</c> ANTES del commit
/// (ver remarks de ese método), aborta la transacción completa — la factura, el movimiento de caja,
/// el egreso de Kardex y el asiento de costo (<c>SalesInvoiceCogsPostingTranslator</c>, mismo
/// evento) nunca llegan a persistirse; el usuario reintenta la autorización. El handler de
/// aplicación (<c>AuthorizeSalesInvoiceHandler</c>) debe capturar este tipo específico (nunca un
/// <c>catch (Exception)</c> genérico) y traducirlo a <c>Result&lt;T&gt;.ValidationFailure</c>.
/// </summary>
public sealed class SalesInvoicePostingFailedException : InvalidOperationException
{
    public string? Code { get; }

    public SalesInvoicePostingFailedException(string message, string? code = null)
        : base(message) => Code = code;
}
