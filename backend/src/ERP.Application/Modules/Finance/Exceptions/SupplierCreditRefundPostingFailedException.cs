namespace ERP.Application.Modules.Finance.Exceptions;

/// <summary>
/// ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — lanzada por
/// <c>SupplierCreditRefundedPostingTranslator</c>/<c>SupplierCreditRefundReversedPostingTranslator</c>
/// cuando el asiento del reembolso (o de su reversa) no puede generarse. Mismo criterio que
/// <c>SupplierPaymentPostingFailedException</c>: un reembolso sin asiento no es un estado válido —
/// lanzar dentro del <c>Handle</c> de un <c>INotificationHandler</c> publicado por
/// <c>ErpDbContext.SaveChangesAsync</c> ANTES del commit aborta la transacción completa (movimiento
/// de <c>SupplierCredit</c>, <c>SupplierCreditRefundTransaction</c> y <c>CashMovement</c> nunca se
/// persisten). Hereda de <see cref="InvalidOperationException"/> para que los handlers de
/// reembolso/reversa la traduzcan a <c>Result&lt;T&gt;.ValidationFailure</c> tras el rollback.
/// </summary>
public sealed class SupplierCreditRefundPostingFailedException : InvalidOperationException
{
    public string? Code { get; }

    public SupplierCreditRefundPostingFailedException(string message, string? code = null)
        : base(message) => Code = code;
}
