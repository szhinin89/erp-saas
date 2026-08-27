namespace ERP.Application.Modules.Expenses.Exceptions;

/// <summary>
/// EXPENSES-CONFIRM-07 — lanzada por <c>ExpenseDocumentConfirmedPostingTranslator</c> cuando
/// <c>IPostingEngine.PostAsync</c> falla. A diferencia de Purchases/Sales (posting falla → warning
/// en log, la confirmación del documento de origen queda igual), Gastos exige que un posting
/// fallido aborte la confirmación completa: lanzar aquí, dentro del <c>Handle</c> de un
/// <c>INotificationHandler</c> publicado por <c>ErpDbContext.SaveChangesAsync</c> ANTES del commit
/// (ver remarks de ese método), hace que la transacción completa (cambio de estado del documento +
/// cualquier entidad en staging) se revierta — el documento de gasto queda en Draft, sin efectos
/// parciales. El handler de aplicación que confirma el documento debe capturar este tipo
/// específico (nunca un <c>catch (Exception)</c> genérico) y traducirlo a
/// <c>Result&lt;T&gt;.ValidationFailure</c>.
/// </summary>
public sealed class ExpensePostingFailedException : InvalidOperationException
{
    public string? Code { get; }

    public ExpensePostingFailedException(string message, string? code = null)
        : base(message) => Code = code;
}
