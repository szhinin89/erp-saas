using System.Xml;
using System.Xml.Schema;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.SchemaValidation;

/// <summary>
/// Valida el XML de Nota de Crédito contra el esquema XSD oficial del SRI
/// (<c>NotaCredito_V1.1.0.xsd</c>). Mismo contrato y misma implementación que
/// <see cref="InvoiceXmlSchemaValidator"/> — solo cambia <see cref="DocumentType"/> y la versión
/// de esquema fija, ambas identidad de esta clase (ADR-031: activación de CreditNote V1.1.0,
/// Fase 11 de P0-01).
/// </summary>
public sealed class CreditNoteXmlSchemaValidator : IElectronicDocumentSchemaValidator
{
    private const string SchemaVersionValue = "1.1.0";

    private readonly IXmlSchemaProvider _schemaProvider;

    public CreditNoteXmlSchemaValidator(IXmlSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public ElectronicDocumentType DocumentType => ElectronicDocumentType.CreditNote;

    public async Task<ElectronicDocumentSchemaValidationResult> ValidateAsync(
        ElectronicDocumentXml xml,
        CancellationToken ct = default
    )
    {
        var schemaSet = await _schemaProvider.GetSchemaSetAsync(
            DocumentType,
            SchemaVersionValue,
            ct
        );
        if (schemaSet is null)
        {
            return new ElectronicDocumentSchemaValidationResult(
                IsValid: false,
                Errors:
                [
                    $"El esquema XSD oficial del SRI para Nota de Crédito {SchemaVersionValue} todavía no está "
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
