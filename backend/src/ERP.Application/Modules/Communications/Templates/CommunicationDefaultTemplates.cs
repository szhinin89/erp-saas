using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Application.Modules.Communications.Templates;

/// <summary>
/// ZH-COMMUNICATIONS-TEMPLATES-01 (ADR-039 D12) — templates por defecto, versionados y embebidos en el
/// release: un despliegue limpio siempre tiene un template válido sin seeds ni BD. Cambiar el texto
/// de salida de un default = nueva versión (las comunicaciones ya encoladas conservan lo renderizado).
/// <para>
/// Solo se registran templates con un productor real. Los propósitos reservados (p. ej.
/// PASSWORD_RESET) no tienen default hasta su fase: encolarlos falla con TEMPLATE_NOT_FOUND.
/// </para>
/// </summary>
public static class CommunicationDefaultTemplates
{
    /// <summary>Idioma de los defaults y de la búsqueda de overrides.</summary>
    public const string Language = "es";

    /// <summary>
    /// v1 = salida exacta del correo armado en código antes de este subsistema (golden en
    /// SalesInvoiceAuthorizedGoldenEmail): mismo texto, mismo orden, sin saltos de línea en el HTML.
    /// </summary>
    public static readonly CommunicationTemplateDefinition SalesInvoiceAuthorizedV1 = new(
        CommunicationPurposes.SalesInvoiceAuthorized,
        Version: 1,
        CommunicationTemplateSource.Default,
        SubjectTemplate: "Factura autorizada {{InvoiceNumber}} - {{IssuerName}}",
        HtmlTemplate: "<p>Estimado/a {{CustomerName}},</p>"
            + "<p>Su factura electronica fue autorizada por el SRI.</p>"
            + "<ul>"
            + "<li><strong>Factura:</strong> {{InvoiceNumber}}</li>"
            + "<li><strong>Clave de acceso:</strong> {{AccessKey}}</li>"
            + "<li><strong>Cliente:</strong> {{CustomerName}}</li>"
            + "<li><strong>Total:</strong> USD {{Total}}</li>"
            + "<li><strong>Emisor:</strong> {{IssuerName}}</li>"
            + "</ul>",
        TextTemplate: "Estimado/a {{CustomerName}},\n\n"
            + "Su factura electronica fue autorizada por el SRI.\n"
            + "Factura: {{InvoiceNumber}}\n"
            + "Clave de acceso: {{AccessKey}}\n"
            + "Cliente: {{CustomerName}}\n"
            + "Total: USD {{Total}}\n"
            + "Emisor: {{IssuerName}}\n",
        Variables:
        [
            new(nameof(SalesInvoiceAuthorizedTemplateModel.CustomerName)),
            new(nameof(SalesInvoiceAuthorizedTemplateModel.InvoiceNumber)),
            new(nameof(SalesInvoiceAuthorizedTemplateModel.AccessKey)),
            new(nameof(SalesInvoiceAuthorizedTemplateModel.Total)),
            new(nameof(SalesInvoiceAuthorizedTemplateModel.IssuerName)),
        ]
    );

    private static readonly IReadOnlyDictionary<string, CommunicationTemplateDefinition> Defaults =
        new[] { SalesInvoiceAuthorizedV1 }.ToDictionary(d => d.Key, StringComparer.Ordinal);

    public static IEnumerable<CommunicationTemplateDefinition> All => Defaults.Values;

    public static CommunicationTemplateDefinition? Find(string templateKey) =>
        Defaults.GetValueOrDefault(templateKey);
}
