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

    /// <summary>ZH-EDOC-COMMUNICATIONS-01 — nota de crédito de venta autorizada (mismo estilo que la factura).</summary>
    public static readonly CommunicationTemplateDefinition SalesCreditNoteAuthorizedV1 = new(
        CommunicationPurposes.SalesCreditNoteAuthorized,
        Version: 1,
        CommunicationTemplateSource.Default,
        SubjectTemplate: "Nota de credito autorizada {{CreditNoteNumber}} - {{IssuerName}}",
        HtmlTemplate: "<p>Estimado/a {{CustomerName}},</p>"
            + "<p>Su nota de credito electronica fue autorizada por el SRI.</p>"
            + "<ul>"
            + "<li><strong>Nota de credito:</strong> {{CreditNoteNumber}}</li>"
            + "<li><strong>Factura modificada:</strong> {{ModifiedInvoiceNumber}}</li>"
            + "<li><strong>Clave de acceso:</strong> {{AccessKey}}</li>"
            + "<li><strong>Cliente:</strong> {{CustomerName}}</li>"
            + "<li><strong>Total:</strong> USD {{Total}}</li>"
            + "<li><strong>Emisor:</strong> {{IssuerName}}</li>"
            + "</ul>",
        TextTemplate: "Estimado/a {{CustomerName}},\n\n"
            + "Su nota de credito electronica fue autorizada por el SRI.\n"
            + "Nota de credito: {{CreditNoteNumber}}\n"
            + "Factura modificada: {{ModifiedInvoiceNumber}}\n"
            + "Clave de acceso: {{AccessKey}}\n"
            + "Cliente: {{CustomerName}}\n"
            + "Total: USD {{Total}}\n"
            + "Emisor: {{IssuerName}}\n",
        Variables:
        [
            new(nameof(SalesCreditNoteAuthorizedTemplateModel.CustomerName)),
            new(nameof(SalesCreditNoteAuthorizedTemplateModel.CreditNoteNumber)),
            new(nameof(SalesCreditNoteAuthorizedTemplateModel.ModifiedInvoiceNumber)),
            new(nameof(SalesCreditNoteAuthorizedTemplateModel.AccessKey)),
            new(nameof(SalesCreditNoteAuthorizedTemplateModel.Total)),
            new(nameof(SalesCreditNoteAuthorizedTemplateModel.IssuerName)),
        ]
    );

    /// <summary>ZH-EDOC-COMMUNICATIONS-01 — comprobante de retención autorizado (al sujeto retenido).</summary>
    public static readonly CommunicationTemplateDefinition RetentionAuthorizedV1 = new(
        CommunicationPurposes.RetentionAuthorized,
        Version: 1,
        CommunicationTemplateSource.Default,
        SubjectTemplate: "Comprobante de retencion autorizado {{RetentionNumber}} - {{IssuerName}}",
        HtmlTemplate: "<p>Estimado/a {{SupplierName}},</p>"
            + "<p>Su comprobante de retencion electronico fue autorizado por el SRI.</p>"
            + "<ul>"
            + "<li><strong>Retencion:</strong> {{RetentionNumber}}</li>"
            + "<li><strong>Documento sustento:</strong> {{SourceDocumentNumber}}</li>"
            + "<li><strong>Clave de acceso:</strong> {{AccessKey}}</li>"
            + "<li><strong>Sujeto retenido:</strong> {{SupplierName}}</li>"
            + "<li><strong>Total retenido:</strong> USD {{TotalRetained}}</li>"
            + "<li><strong>Agente de retencion:</strong> {{IssuerName}}</li>"
            + "</ul>",
        TextTemplate: "Estimado/a {{SupplierName}},\n\n"
            + "Su comprobante de retencion electronico fue autorizado por el SRI.\n"
            + "Retencion: {{RetentionNumber}}\n"
            + "Documento sustento: {{SourceDocumentNumber}}\n"
            + "Clave de acceso: {{AccessKey}}\n"
            + "Sujeto retenido: {{SupplierName}}\n"
            + "Total retenido: USD {{TotalRetained}}\n"
            + "Agente de retencion: {{IssuerName}}\n",
        Variables:
        [
            new(nameof(RetentionAuthorizedTemplateModel.SupplierName)),
            new(nameof(RetentionAuthorizedTemplateModel.RetentionNumber)),
            new(nameof(RetentionAuthorizedTemplateModel.SourceDocumentNumber)),
            new(nameof(RetentionAuthorizedTemplateModel.AccessKey)),
            new(nameof(RetentionAuthorizedTemplateModel.TotalRetained)),
            new(nameof(RetentionAuthorizedTemplateModel.IssuerName)),
        ]
    );

    private static readonly IReadOnlyDictionary<string, CommunicationTemplateDefinition> Defaults =
        new[]
        {
            SalesInvoiceAuthorizedV1,
            SalesCreditNoteAuthorizedV1,
            RetentionAuthorizedV1,
        }.ToDictionary(d => d.Key, StringComparer.Ordinal);

    public static IEnumerable<CommunicationTemplateDefinition> All => Defaults.Values;

    public static CommunicationTemplateDefinition? Find(string templateKey) =>
        Defaults.GetValueOrDefault(templateKey);
}
