using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using System.Xml;
using System.Xml.Schema;

namespace ERP.Application.Modules.ElectronicDocuments.SchemaValidation;

/// <summary>
/// RETENTIONS-SRI-SCHEMA-VALIDATOR-04C — valida el XML de Comprobante de Retención contra el
/// esquema XSD oficial del SRI (<c>ComprobanteRetencion_V1.0.0.xsd</c>). Mismo contrato y misma
/// implementación que <see cref="InvoiceXmlSchemaValidator"/>/<see cref="CreditNoteXmlSchemaValidator"/>
/// — solo cambia <see cref="DocumentType"/> y la versión de esquema fija, ambas identidad de esta
/// clase.
///
/// <see cref="SchemaVersionValue"/> es "1.0.0", no "2.0.0" — misma versión que ya usa
/// <c>RetentionXmlBuilder</c> (RETENTIONS-SRI-XML-MAPPER-03B), por la misma razón documentada
/// ahí: V2.0.0 exige <c>parteRel</c>/<c>pagoLocExt</c>/<c>impuestosDocSustento</c>, campos que
/// <c>RetentionElectronicDocumentData</c> no modela y que inventar violaría la regla de no
/// quemar datos de negocio. Este validador y el builder deben permanecer sincronizados en la
/// misma versión de esquema — si una fase futura migra a V2.0.0, ambos cambian juntos.
///
/// Este validador NO se activa todavía en <c>manifest.json</c> (<c>Retention.activeVersion</c>
/// permanece <c>null</c>) — esa activación es responsabilidad de una fase posterior
/// (RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B, sección J), una vez exista también el wiring
/// completo (<c>IElectronicDocumentXmlSupplier</c>) y una autorización real verificada.
/// </summary>
public sealed class RetentionXmlSchemaValidator : IElectronicDocumentSchemaValidator
{
    private const string SchemaVersionValue = "1.0.0";

    private readonly IXmlSchemaProvider _schemaProvider;

    public RetentionXmlSchemaValidator(IXmlSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public ElectronicDocumentType DocumentType => ElectronicDocumentType.Retention;

    public async Task<ElectronicDocumentSchemaValidationResult> ValidateAsync(
        ElectronicDocumentXml xml,
        CancellationToken ct = default
    )
    {
        var schemaSet = await _schemaProvider.GetSchemaSetAsync(DocumentType, SchemaVersionValue, ct);
        if (schemaSet is null)
        {
            return new ElectronicDocumentSchemaValidationResult(
                IsValid: false,
                Errors:
                [
                    $"El esquema XSD oficial del SRI para Comprobante de Retención {SchemaVersionValue} todavía no está "
                        + "incorporado al proyecto. El XML no pudo validarse — la firma electrónica no debe ejecutarse.",
                ],
                Warnings: [],
                SchemaVersion: SchemaVersionValue,
                DocumentType: DocumentType
            );
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemaSet,
        };
        settings.ValidationEventHandler += (_, e) =>
        {
            (e.Severity == XmlSeverityType.Warning ? warnings : errors).Add(e.Message);
        };

        try
        {
            using var stringReader = new StringReader(xml.Xml);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            while (xmlReader.Read())
            {
                // XmlReaderSettings.ValidationEventHandler recopila todos los errores del
                // documento en esta única pasada — no se detiene en el primero.
            }
        }
        catch (XmlException ex)
        {
            errors.Add($"El XML no está bien formado: {ex.Message}");
        }

        return new ElectronicDocumentSchemaValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors,
            Warnings: warnings,
            SchemaVersion: SchemaVersionValue,
            DocumentType: DocumentType
        );
    }
}
