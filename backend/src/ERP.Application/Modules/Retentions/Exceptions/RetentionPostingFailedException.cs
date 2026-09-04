namespace ERP.Application.Modules.Retentions.Exceptions;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-2 — mismo criterio que
/// <c>ERP.Application.Modules.Expenses.Exceptions.ExpensePostingFailedException</c>
/// (EXPENSES-CONFIRM-07, "posting estricto"): si el asiento de la retención no puede generarse,
/// <see cref="Modules.Accounting.Posting.Translators.RetentionDocumentIssuedPostingTranslator"/>
/// LANZA esta excepción en vez de solo loguear un warning. Se propaga desde el <c>Publish()</c>
/// interno de <c>ErpDbContext.SaveChangesAsync</c>, que hace rollback completo de la transacción
/// ANTES de que el handler que confirma el documento origen (Gasto) la capture — el resultado es
/// que ni el documento origen, ni el <c>AccountsPayable</c>, ni el <c>RetentionDocument</c> llegan
/// a persistirse (docs/decisions/RETENTIONS-MODULE-DESIGN-01.md § "Impacto contable": "una
/// retención sin asiento sería un pasivo fiscal fantasma").
/// </summary>
public sealed class RetentionPostingFailedException : InvalidOperationException
{
    public string? Code { get; }

    public RetentionPostingFailedException(string message, string? code = null)
        : base(message) => Code = code;
}
