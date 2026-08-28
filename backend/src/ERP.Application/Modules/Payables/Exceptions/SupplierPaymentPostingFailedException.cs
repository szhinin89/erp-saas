namespace ERP.Application.Modules.Payables.Exceptions;

/// <summary>
/// SUPPLIER-PAYMENTS-POSTING-15D — lanzada por <c>SupplierPaymentConfirmedPostingTranslator</c>
/// cuando <c>IPostingEngine.PostAsync</c> falla o una <c>CompanyFinancialDestination</c> referenciada
/// ya no es postable. Mismo criterio que <c>ExpensePostingFailedException</c> (Gastos): un pago a
/// proveedor sin asiento contable no es un estado válido — "no confirmar pago sin asiento" — lanzar
/// aquí, dentro del <c>Handle</c> de un <c>INotificationHandler</c> publicado por
/// <c>ErpDbContext.SaveChangesAsync</c> ANTES del commit, aborta la transacción completa (el
/// <c>SupplierPayment</c> y los saldos de <c>AccountsPayableInstallment</c> ya mutados en memoria
/// nunca llegan a persistirse). El handler de aplicación que registra el pago debe capturar este
/// tipo específico (nunca un <c>catch (Exception)</c> genérico) y traducirlo a
/// <c>Result&lt;T&gt;.ValidationFailure</c>.
/// </summary>
public sealed class SupplierPaymentPostingFailedException : InvalidOperationException
{
    public string? Code { get; }

    public SupplierPaymentPostingFailedException(string message, string? code = null)
        : base(message) => Code = code;
}
