using ERP.Domain.Modules.Communications.Constants;

namespace ERP.Application.Modules.Communications.Templates;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — variables de RETENTION_AUTHORIZED (destinatario: sujeto retenido), ya
/// formateadas (<paramref name="TotalRetained"/> con dos decimales, InvariantCulture).
/// </summary>
public sealed record RetentionAuthorizedTemplateModel(
    string SupplierName,
    string RetentionNumber,
    string SourceDocumentNumber,
    string AccessKey,
    string TotalRetained,
    string IssuerName
) : ICommunicationTemplateModel
{
    public string TemplateKey => CommunicationPurposes.RetentionAuthorized;

    public IReadOnlyDictionary<string, string?> ToVariables() =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [nameof(SupplierName)] = SupplierName,
            [nameof(RetentionNumber)] = RetentionNumber,
            [nameof(SourceDocumentNumber)] = SourceDocumentNumber,
            [nameof(AccessKey)] = AccessKey,
            [nameof(TotalRetained)] = TotalRetained,
            [nameof(IssuerName)] = IssuerName,
        };
}
