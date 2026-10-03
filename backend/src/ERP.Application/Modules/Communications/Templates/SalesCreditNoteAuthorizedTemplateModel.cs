using ERP.Domain.Modules.Communications.Constants;

namespace ERP.Application.Modules.Communications.Templates;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — variables de SALES_CREDIT_NOTE_AUTHORIZED, ya formateadas
/// (<paramref name="Total"/> con dos decimales, InvariantCulture).
/// </summary>
public sealed record SalesCreditNoteAuthorizedTemplateModel(
    string CustomerName,
    string CreditNoteNumber,
    string ModifiedInvoiceNumber,
    string AccessKey,
    string Total,
    string IssuerName
) : ICommunicationTemplateModel
{
    public string TemplateKey => CommunicationPurposes.SalesCreditNoteAuthorized;

    public IReadOnlyDictionary<string, string?> ToVariables() =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [nameof(CustomerName)] = CustomerName,
            [nameof(CreditNoteNumber)] = CreditNoteNumber,
            [nameof(ModifiedInvoiceNumber)] = ModifiedInvoiceNumber,
            [nameof(AccessKey)] = AccessKey,
            [nameof(Total)] = Total,
            [nameof(IssuerName)] = IssuerName,
        };
}
