using ERP.Domain.Modules.Communications.Constants;

namespace ERP.Application.Modules.Communications.Templates;

/// <summary>
/// Variables de SALES_INVOICE_AUTHORIZED: solo lo que el template usa hoy, ya formateado
/// (<paramref name="Total"/> con dos decimales, InvariantCulture). Sin agregados ni navegación.
/// </summary>
public sealed record SalesInvoiceAuthorizedTemplateModel(
    string CustomerName,
    string InvoiceNumber,
    string AccessKey,
    string Total,
    string IssuerName
) : ICommunicationTemplateModel
{
    public string TemplateKey => CommunicationPurposes.SalesInvoiceAuthorized;

    public IReadOnlyDictionary<string, string?> ToVariables() =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [nameof(CustomerName)] = CustomerName,
            [nameof(InvoiceNumber)] = InvoiceNumber,
            [nameof(AccessKey)] = AccessKey,
            [nameof(Total)] = Total,
            [nameof(IssuerName)] = IssuerName,
        };
}
