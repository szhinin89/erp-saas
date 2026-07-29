using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using System.Xml;
using System.Xml.Schema;

namespace ERP.Application.Modules.ElectronicDocuments.SchemaValidation;

/// <summary>
/// Valida el XML de Factura contra el esquema XSD oficial del SRI. Completamente desacoplado
/// del origen del XML — recibe únicamente <see cref="ElectronicDocumentXml"/>, nunca
/// SalesInvoice ni ningún módulo de negocio.
///
/// <see cref="SchemaVersion"/> ("1.1.0") es la identidad fija de este validador — misma
/// convención que <c>InvoiceXmlBuilder.CodDoc</c>: no es un dato leído de ninguna parte, es
/// qué versión de esquema este validador sabe aplicar. Una futura versión 1.2.0 se resuelve
/// con un segundo <see cref="IElectronicDocumentSchemaValidator"/> registrado en DI, o
/// reutilizando esta misma clase si el SRI mantiene la misma versión pero cambia el archivo
/// XSD — el contrato no necesita cambiar en ninguno de los dos casos.
/// </summary>
public sealed class InvoiceXmlSchemaValidator : IElectronicDocumentSchemaValidator
{
    private const string SchemaVersionValue = "1.1.0";

    private readonly IXmlSchemaProvider _schemaProvider;

    public InvoiceXmlSchemaValidator(IXmlSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public ElectronicDocumentType DocumentType => ElectronicDocumentType.Invoice;

    public async Task<ElectronicDocumentSchemaValidationResult> ValidateAsync(
        ElectronicDocumentXml xml, CancellationToken ct = default)
    {
        var schemaSet = await _schemaProvider.GetSchemaSetAsync(DocumentType, SchemaVersionValue, ct);
        if (schemaSet is null)
        {
            return new ElectronicDocumentSchemaValidationResult(
                IsValid: false,
                Errors:
                [
                    $"El esquema XSD oficial del SRI para Factura {SchemaVersionValue} todavía no está " +
                    "incorporado al proyecto. El XML no pudo validarse — la firma electrónica no debe ejecutarse.",
                ],
                Warnings: [],
                SchemaVersion: SchemaVersionValue,
                DocumentType: DocumentType);
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
            DocumentType: DocumentType);
    }
}
